using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using RedisCache.Attributes;
using RedisCache.RedisSever;
using System.Collections.Concurrent;
using System.Threading.Tasks.Dataflow;

namespace RedisCache;

/// <summary>
/// Redis 缓存组件 — 缓冲写入 Redis，达到阈值后通过回调刷入数据库
/// </summary>
public class RedisCache : IRedisCache
{
    private readonly Block block;
    private readonly IRedisService redisService;
    private readonly RedisFlushHandler? onFlush;
    private readonly IServiceProvider serviceProvider;
    private readonly int threshold;
    private readonly bool isPolling;
    private bool isPollingStarted;
    private readonly TimeSpan interval;
    private readonly List<string> redisKeys;
    private int pollingCount;
    private readonly ILogger<RedisCache> _logger;

    // 本地计数器：避免每次写入都查询 Redis
    private readonly ConcurrentDictionary<string, int> localCounters = new();

    // 防重复刷盘：同一 key 同时只允许一个刷盘任务
    private readonly ConcurrentDictionary<string, byte> flushingKeys = new();

    public RedisCache(
        IOptionsMonitor<RedisOption> options,
        IRedisService redisService,
        IServiceProvider serviceProvider,
        ILogger<RedisCache> logger,
        RedisFlushHandler? onFlush = null)
    {
        this.redisService = redisService;
        this.serviceProvider = serviceProvider;
        this.onFlush = onFlush;
        _logger = logger;

        threshold = options.CurrentValue.Threshold;
        isPolling = options.CurrentValue.IsPolling;
        interval = TimeSpan.FromSeconds(options.CurrentValue.Interval);

        block = new Block(WriteRedisAsync, WriteDataBaseAsync);
        redisKeys = RedisKeyExtension.GetEntityKeys();

        if (onFlush == null && !isPolling)
            _logger.LogWarning("No flush handler and polling disabled — data will accumulate in Redis indefinitely");

        _ = PollingThread();
    }

    // ==================== 公开 API ====================

    public async Task<bool> PostRedisAsync(object value)
    {
        var key = value.GetType().Name;
        try
        {
            var actionBlock = block.GetBlock(key);
            return await actionBlock.SendAsync(value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PostRedisAsync failed for key {Key}", key);
            return false;
        }
    }

    public async Task<bool> PostRedisAsync(List<object> values)
    {
        if (values == null || values.Count == 0)
        {
            _logger.LogWarning("PostRedisAsync called with empty or null list");
            return false;
        }

        var key = values[0].GetType().Name;
        try
        {
            var actionBlock = block.GetBlock(key);
            foreach (var value in values)
            {
                var accepted = await actionBlock.SendAsync(value);
                if (!accepted)
                    _logger.LogWarning("ActionBlock rejected message for key {Key} — buffer full", key);
            }
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PostRedisAsync (batch) failed for key {Key}", key);
            return false;
        }
    }

    // ==================== 内部：Redis 写入 ====================

    internal async Task WriteRedisAsync(object value)
    {
        var key = value.GetType().Name;
        try
        {
            var serialized = JsonConvert.SerializeObject(value);
            var redisKey = value.GetRedisKey();

            var currentCount = localCounters.AddOrUpdate(key, 1, (_, c) => c + 1);

            var isSuccess = await redisService.HashSetFieldAsync(key, new ConcurrentDictionary<string, string>
            {
                [redisKey] = serialized
            });

            if (!isSuccess)
            {
                // Redis 写入失败：回退计数，放入重试队列
                localCounters.AddOrUpdate(key, 0, (_, c) => Math.Max(0, c - 1));

                if (block.GetRetryCount(key) < 3)
                    await block.ResendAsync(key, value);
                else
                    block.TryRemove(key);

                return;
            }

            // 达到阈值 → 触发刷盘
            if (!isPolling && currentCount >= threshold)
            {
                if (flushingKeys.TryAdd(key, 0))
                {
                    try { await WriteDataBaseAsync(key); }
                    finally { flushingKeys.TryRemove(key, out _); }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WriteRedisAsync failed for key {Key}", key);
        }
    }

    // ==================== 内部：数据刷盘（核心） ====================

    /// <summary>
    /// 刷盘流程：
    /// ① 读取 Redis Hash → ② 回调写入数据库 → ③ 成功才删除 Redis 数据
    /// <br/>
    /// 核心原则：<b>数据库写入失败时，Redis 数据原封不动，等待下次重试。</b>
    /// 宁可下次重复刷盘，绝不丢弃一条数据。
    /// </summary>
    internal async Task<bool> WriteDataBaseAsync(string tKey)
    {
        if (onFlush == null)
        {
            _logger.LogDebug("No flush handler registered, skipping flush for key {Key}", tKey);
            return true;
        }

        var lockValue = Guid.NewGuid().ToString();
        var cts = new CancellationTokenSource();
        Task? renewalTask = null;

        try
        {
            // ① 阈值检查
            if (localCounters.GetOrAdd(tKey, 0) < threshold)
                return false;

            // ② 获取分布式锁（重试 + 指数退避）
            if (!await redisService.AcquireLockWithRetryAsync(tKey, lockValue, maxRetries: 3, baseDelayMs: 100))
                return false;

            // ③ 启动锁续期
            renewalTask = Task.Run(async () =>
            {
                try { await redisService.RenewalLockAsync(tKey, lockValue, cts.Token); }
                catch (OperationCanceledException) { }
                catch (Exception ex) { _logger.LogError(ex, "Lock renewal failed for key {Key}", tKey); }
            }, cts.Token);

            // ④ HSCAN 分页读取 Redis Hash
            var hashValues = await redisService.HashGetAsync(tKey);
            if (hashValues == null || hashValues.IsEmpty)
            {
                localCounters[tKey] = 0;
                return true;
            }

            // ⑤ 反序列化
            var fieldKeys = hashValues.Select(v => v.Key).ToList();
            var entityType = tKey.GetRedisEntity();
            var entities = hashValues.Select(kvp =>
                JsonConvert.DeserializeObject(kvp.Value, entityType)!).ToList();

            // ⑥ 回调写入数据库
            _logger.LogDebug("Flushing {Count} entities for key {Key}", entities.Count, tKey);
            var flushSuccess = await onFlush(serviceProvider, tKey, entities);

            if (!flushSuccess)
            {
                // ⚠️ 数据库写入失败 → Redis 数据原封不动，下次写入或轮询时自动重试
                _logger.LogWarning("Flush handler returned false for key {Key}. " +
                    "{Count} entities retained in Redis for retry.", tKey, entities.Count);
                return false;
            }

            // ⑦ 数据库成功 → 删除 Redis 数据
            var deletedCount = await redisService.HashDeleteFieldsAsync(tKey, fieldKeys);
            if (deletedCount != fieldKeys.Count)
            {
                _logger.LogWarning("Deleted {Actual}/{Expected} fields for key {Key}. " +
                    "Remaining fields will be flushed on next attempt.", deletedCount, fieldKeys.Count, tKey);
            }

            // ⑧ 重置计数器：减去已删除数，保留刷盘期间新写入的增量
            localCounters.AddOrUpdate(tKey, 0, (_, c) => Math.Max(0, c - fieldKeys.Count));

            _logger.LogDebug("Flush completed for key {Key}: {Count} entities", tKey, entities.Count);
            return true;
        }
        catch (Exception ex)
        {
            // ⚠️ 任何异常 → Redis 数据原封不动，下次重试
            _logger.LogError(ex, "WriteDataBaseAsync failed for key {Key}. Data retained in Redis.", tKey);
            return false;
        }
        finally
        {
            cts.Cancel();
            if (renewalTask != null)
            {
                try { await renewalTask; }
                catch (Exception ex) { _logger.LogError(ex, "Error awaiting renewal for key {Key}", tKey); }
            }
            await redisService.ReleaseLockAsync(tKey, lockValue);
        }
    }

    // ==================== 内部：轮询 ====================

    private async Task PollingThread(CancellationToken cancellationToken = default)
    {
        if (!isPolling || isPollingStarted || redisKeys.Count == 0)
            return;

        isPollingStarted = true;
        using var timer = new PeriodicTimer(interval);

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                pollingCount++;
                _logger.LogDebug("Polling round {Count} started", pollingCount);

                await Parallel.ForEachAsync(redisKeys, cancellationToken, async (redisKey, ct) =>
                {
                    try
                    {
                        var actualLength = await redisService.GetHashLength(redisKey);
                        localCounters[redisKey] = (int)actualLength;
                        await block.DoPollingAsync(redisKey);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Polling failed for key {RedisKey}", redisKey);
                    }
                });
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Polling cancelled");
        }
        finally
        {
            pollingCount = 0;
            isPollingStarted = false;
        }
    }
}

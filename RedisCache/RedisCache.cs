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

    // 本地计数器：避免每次写入都调用 Redis GetHashLength（减少网络往返）
    private readonly ConcurrentDictionary<string, int> localCounters = new();

    // 正在刷盘中的 key 集合（防止并发重复刷盘）
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
                {
                    _logger.LogWarning("ActionBlock rejected message for key {Key} — buffer full, applying backpressure", key);
                }
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
            // 序列化必须在递增计数器之前（防止序列化失败导致计数错误）
            var serialized = JsonConvert.SerializeObject(value);
            var redisKey = value.GetRedisKey();

            // 原子递增本地计数器（替代 Redis GetHashLength 网络调用）
            var currentCount = localCounters.AddOrUpdate(key, 1, (_, c) => c + 1);

            var isSuccess = await redisService.HashSetFieldAsync(key, new ConcurrentDictionary<string, string>
            {
                [redisKey] = serialized
            });

            if (!isSuccess)
            {
                // Redis 写入失败：回退计数器
                localCounters.AddOrUpdate(key, 0, (_, c) => Math.Max(0, c - 1));

                if (block.GetRetryCount(key) < 3)
                    await block.ResendAsync(key, value);
                else
                    block.TryRemove(key);

                return;
            }

            // 成功：阈值检查 & 触发刷盘
            if (!isPolling && currentCount >= threshold)
            {
                // 避免重复触发：同一时刻只允许一个刷盘任务
                if (flushingKeys.TryAdd(key, 0))
                {
                    try
                    {
                        await WriteDataBaseAsync(key);
                    }
                    finally
                    {
                        flushingKeys.TryRemove(key, out _);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WriteRedisAsync failed for key {Key}", key);
        }
    }

    // ==================== 内部：数据刷盘 ====================

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
            // 使用本地计数器（避免了 Redis GetHashLength 网络往返）
            if (localCounters.GetOrAdd(tKey, 0) < threshold)
                return false;

            // 分布式锁（带重试，适应多进程竞争）
            if (!await redisService.AcquireLockWithRetryAsync(tKey, lockValue, maxRetries: 3, baseDelayMs: 100))
                return false;

            // 启动锁续期
            renewalTask = Task.Run(async () =>
            {
                try
                {
                    await redisService.RenewalLockAsync(tKey, lockValue, cts.Token);
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lock renewal failed for key {Key}", tKey);
                }
            }, cts.Token);

            // HSCAN 分页读取 Hash 数据（优化：先读后删，避免数据重复）
            var hashValues = await redisService.HashGetAsync(tKey);
            if (hashValues == null || hashValues.IsEmpty)
            {
                localCounters[tKey] = 0; // 重置计数器
                return true;
            }

            // === 幂等性优化：先删 Redis 数据，再刷数据库 ===
            // 如果刷 DB 失败，数据还在 Redis 中（通过锁保护）；
            // 删完 Redis 后刷 DB 成功 → 完美；
            // 删完 Redis 后进程崩溃 → 数据丢失风险（需要在 Redis 删除前先备份）
            var fieldKeys = hashValues.Select(v => v.Key).ToList();

            // 先收集数据，再删除
            var entityType = tKey.GetRedisEntity();
            var entities = hashValues.Select(kvp =>
                JsonConvert.DeserializeObject(kvp.Value, entityType)!).ToList();

            // 先删除 Redis 数据（防止并发写入导致重复）
            // 注意：如果下面回调失败，这 N 条数据将丢失。
            // 如果数据绝不能丢失，可改为"先刷后删" + 幂等去重
            await redisService.HashDeleteFieldsAsync(tKey, fieldKeys);

            // 重置本地计数器
            localCounters[tKey] = 0;

            // 刷入数据库
            var flushSuccess = await onFlush(serviceProvider, tKey, entities);
            if (!flushSuccess)
            {
                _logger.LogWarning("Flush handler returned false for key {Key} — {Count} entities already removed from Redis", tKey, entities.Count);
                return false;
            }

            _logger.LogDebug("Flushed {Count} entities for key {Key}", entities.Count, tKey);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WriteDataBaseAsync failed for key {Key}", tKey);
            return false;
        }
        finally
        {
            cts.Cancel();
            if (renewalTask != null)
            {
                try { await renewalTask; }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error awaiting lock renewal completion for key {Key}", tKey);
                }
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

                // 并行轮询所有 entity 类型（利用多核）
                await Parallel.ForEachAsync(redisKeys, cancellationToken, async (redisKey, ct) =>
                {
                    try
                    {
                        // 同步本地计数器与 Redis 实际长度
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

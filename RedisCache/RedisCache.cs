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
                    _logger.LogWarning("ActionBlock rejected message for key {Key}", key);
            }
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PostRedisAsync (batch) failed for key {Key}", key);
            return false;
        }
    }

    // ==================== 内部：Redis 写入（Dataflow ActionBlock 回调） ====================

    internal async Task WriteRedisAsync(object value)
    {
        var key = value.GetType().Name;
        try
        {
            if (!isPolling)
                await WriteDataBaseAsync(key);

            var redisKey = value.GetRedisKey();
            var isSuccess = await redisService.HashSetFieldAsync(key, new ConcurrentDictionary<string, string>
            {
                [redisKey] = JsonConvert.SerializeObject(value)
            });

            if (!isSuccess && block.GetRetryCount(key) < 3)
                await block.ResendAsync(key, value);
            else
                block.TryRemove(key);
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
            if (await redisService.GetHashLength(tKey) < threshold)
                return false;

            if (!await redisService.AcquireLockAsync(tKey, lockValue))
                return false;

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

            var hashValues = await redisService.HashGetAsync(tKey);
            if (hashValues == null || hashValues.IsEmpty)
                return true;

            var entityType = tKey.GetRedisEntity();
            var entities = hashValues.Select(kvp =>
                JsonConvert.DeserializeObject(kvp.Value, entityType)!).ToList();

            // 通过回调委托将数据交给使用者处理
            var flushSuccess = await onFlush(serviceProvider, tKey, entities);
            if (!flushSuccess)
            {
                _logger.LogWarning("Flush handler returned false for key {Key}", tKey);
                return false;
            }

            var deleteCount = await redisService.HashDeleteFieldsAsync(tKey, hashValues.Select(v => v.Key));
            _logger.LogDebug("Flushed {Count} entities for key {Key}, deleted {Deleted} from Redis",
                entities.Count, tKey, deleteCount);

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

                foreach (var redisKey in redisKeys)
                {
                    try
                    {
                        await block.DoPollingAsync(redisKey);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Polling failed for key {RedisKey}", redisKey);
                    }
                }
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

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System.Collections.Concurrent;

namespace RedisCache.RedisSever;

public class RedisService : IRedisService, IDisposable
{
    private readonly ConnectionMultiplexer _conn;
    private readonly IDatabase _db;
    private readonly string LockKey;
    private readonly int Expiry;
    private readonly string SlotTag = "{SlotTag}:";
    private readonly ILogger<RedisService> _logger;

    // 大 Hash 分页读取每批大小
    private const int HashScanPageSize = 1000;

    public RedisService(IOptionsMonitor<RedisOption> options, ILogger<RedisService> logger)
        : this(options.CurrentValue, logger)
    {
    }

    public RedisService(RedisOption options, ILogger<RedisService> logger)
    {
        _conn = ConnectionMultiplexer.Connect(options.ConnectionString);
        _db = _conn.GetDatabase(options.DbNumber);
        LockKey = options.LockKey;
        Expiry = options.Expiry;
        _logger = logger;
    }

    #region Hash

    public async Task<ConcurrentDictionary<string, string>> HashGetAsync(string key)
    {
        // HSCAN 分页读取，避免大 Hash 一次性加载全部到内存
        var result = new ConcurrentDictionary<string, string>();

        // pattern: default(RedisValue) = 匹配所有 fields
        await foreach (var entry in _db.HashScanAsync(key, default, HashScanPageSize))
        {
            result[entry.Name!] = entry.Value!;
        }

        return result;
    }

    public async Task<ConcurrentDictionary<string, string>> HashGetFieldsAsync(string key, IEnumerable<string> fields)
    {
        return (await _db.HashGetAsync(key, fields.ToRedisValues())).ToConcurrentDictionary(fields);
    }

    public async Task<ConcurrentDictionary<string, string>> HashGetFieldAsync(string key, string fields)
    {
        return (await _db.HashGetAsync(key, fields.ToRedisValue())).ToConcurrentDictionary(fields);
    }

    public async Task HashSetAsync(string key, ConcurrentDictionary<string, string> entries)
    {
        var val = entries.ToHashEntries();
        if (val.Length > 0)
            await _db.HashSetAsync(key, val);
    }

    public async Task HashSetFieldsAsync(string key, ConcurrentDictionary<string, string> fields)
    {
        if (fields == null || fields.IsEmpty)
            return;

        var hs = await HashGetAsync(key);
        foreach (var field in fields)
            hs[field.Key] = field.Value;

        await HashSetAsync(key, hs);
    }

    /// <summary>
    /// 写入单个 field 到 Hash — 跳过 Dictionary 和 HashEntry[] 的中间分配
    /// </summary>
    public async Task<bool> HashSetFieldAsync(string key, ConcurrentDictionary<string, string> fields)
    {
        try
        {
            // 单 field 优化：直接 HSET，跳过字典→数组转换
            if (fields != null && fields.Count == 1)
            {
                var (field, value) = fields.First();
                await _db.HashSetAsync(key, field, value);
            }
            else if (fields != null && !fields.IsEmpty)
            {
                await HashSetAsync(key, fields);
            }

            _logger.LogDebug("HashSetFieldAsync succeeded for key {Key}", key);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HashSetFieldAsync failed for key {Key}", key);
            return false;
        }
    }

    public async Task<bool> HashFieldsExistsAsync(string key, IEnumerable<string> fields)
    {
        if (!await KeyExistsAsync(key))
            return false;

        var dic = await HashGetFieldsAsync(key, fields);
        if (dic == null || dic.IsEmpty)
            return false;

        foreach (var field in fields)
        {
            if (!dic.ContainsKey(field) || dic[field] == null)
                return false;
        }
        return true;
    }

    public async Task<long> HashDeleteFieldsAsync(string key, IEnumerable<string> fields)
    {
        if (fields == null || !fields.Any())
            return -1;

        var count = await _db.HashDeleteAsync(key, fields.ToRedisValues());
        return count;
    }

    #endregion

    #region Key

    public async Task<long> GetHashLength(string key)
    {
        if (!await KeyExistsAsync(key))
            return 0;
        return await _db.HashLengthAsync(key);
    }

    public async Task<bool> SetKeyExpireAsync(string key, TimeSpan expiry)
    {
        return await _db.KeyExpireAsync(key, expiry);
    }

    public async Task<bool> KeyExistsAsync(string key)
    {
        return await _db.KeyExistsAsync(key);
    }

    public async Task<long> KeyDeleteAsync(IEnumerable<string> keys)
    {
        return await _db.KeyDeleteAsync(keys.Select(k => (RedisKey)k).ToArray());
    }

    public async Task<bool> KeyDeleteAsync(string key)
    {
        return await _db.KeyDeleteAsync(key);
    }

    #endregion

    #region Lock

    // ==================== 使用 Redis TIME 避免时钟偏差 ====================
    // 不再依赖各服务器的本地时间，统一使用 Redis 服务器时间作为权威时钟

    private const string AcquireLockScript = @"
        local lockKey = KEYS[1]
        local key = KEYS[2]
        local lockValue = ARGV[1]
        local expiryMs = tonumber(ARGV[2])
        -- 使用 Redis TIME 返回的秒级时间戳 * 1000 + 微秒/1000
        local currentTime = redis.call('TIME')
        local now = tonumber(currentTime[1]) * 1000 + math.floor(tonumber(currentTime[2]) / 1000)

        local value = redis.call('HGET', lockKey, key)
        if not value or tonumber(value:match(':(%d+)$')) < now then
            local newExpiry = now + expiryMs
            redis.call('HSET', lockKey, key, lockValue .. ':' .. newExpiry)
        return true
        end
        return false";

    private const string ReleaseLockScript = @"
        local lockKey = KEYS[1]
        local key = KEYS[2]
        local expectedValue = ARGV[1]

        local currentValue = redis.call('HGET', lockKey, key)
        if currentValue and currentValue:match('^(.-):') == expectedValue then
            redis.call('HDEL', lockKey, key)
        return true
        end
        return false";

    private const string RenewalLockScript = @"
        local lockKey = KEYS[1]
        local key = KEYS[2]
        local lockValue = ARGV[1]
        local expiryMs = tonumber(ARGV[2])
        local currentTime = redis.call('TIME')
        local now = tonumber(currentTime[1]) * 1000 + math.floor(tonumber(currentTime[2]) / 1000)

        local currentValue = redis.call('HGET', lockKey, key)
        if currentValue then
            local valuePart = currentValue:match('^(.-):')
            if valuePart == lockValue then
                local oldExpiry = tonumber(currentValue:match(':(%d+)$'))
                if oldExpiry and oldExpiry >= now then
                    local newExpiry = now + expiryMs
                    redis.call('HSET', lockKey, key, lockValue .. ':' .. newExpiry)
                    return true
                end
            end
        end
        return false";

    public async Task<bool> AcquireLockAsync(string key, string lockValue)
    {
        try
        {
            // 传入 Expiry 毫秒数（Seconds → ms），由 Lua 脚本用 Redis TIME 计算绝对过期时间
            var expiryMs = Expiry * 1000;

            var result = await _db.ScriptEvaluateAsync(AcquireLockScript,
                new RedisKey[] { SlotTag + LockKey, SlotTag + key },
                new RedisValue[] { lockValue, expiryMs });

            var isAcquire = (bool)result;
            if (!isAcquire)
                _logger.LogWarning("Failed to acquire lock for key {Key}", key);

            return isAcquire;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AcquireLockAsync failed for key {Key}", key);
            return false;
        }
    }

    /// <summary>
    /// 获取锁（带重试和指数退避）
    /// </summary>
    public async Task<bool> AcquireLockWithRetryAsync(string key, string lockValue, int maxRetries = 3, int baseDelayMs = 100)
    {
        for (int i = 0; i < maxRetries; i++)
        {
            if (await AcquireLockAsync(key, lockValue))
                return true;

            if (i < maxRetries - 1)
            {
                // 指数退避：100ms, 200ms, 400ms...
                var delay = baseDelayMs * (int)Math.Pow(2, i);
                _logger.LogDebug("Lock retry {Retry}/{Max} for key {Key}, waiting {Delay}ms", i + 1, maxRetries, key, delay);
                await Task.Delay(delay);
            }
        }

        _logger.LogWarning("Failed to acquire lock after {Retries} retries for key {Key}", maxRetries, key);
        return false;
    }

    public async Task<bool> ReleaseLockAsync(string key, string lockValue)
    {
        try
        {
            var result = await _db.ScriptEvaluateAsync(ReleaseLockScript,
                new RedisKey[] { SlotTag + LockKey, SlotTag + key },
                new RedisValue[] { lockValue });

            var isRelease = (bool)result;
            if (!isRelease)
                _logger.LogWarning("Failed to release lock for key {Key} — may have expired or been held by another", key);

            return isRelease;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ReleaseLockAsync failed for key {Key}", key);
            return false;
        }
    }

    public async Task RenewalLockAsync(string key, string lockValue, CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(Expiry - 2, 1)));
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                var expiryMs = Expiry * 1000;

                var result = await _db.ScriptEvaluateAsync(RenewalLockScript,
                    new RedisKey[] { SlotTag + LockKey, SlotTag + key },
                    new RedisValue[] { lockValue, expiryMs });

                var renewed = (bool)result;
                if (!renewed)
                {
                    _logger.LogWarning("Lock renewal failed for key {Key} — lock may have been acquired by another instance", key);
                    break;
                }

                _logger.LogTrace("Lock renewed for key {Key}", key);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Lock renewal cancelled for key {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lock renewal error for key {Key}", key);
        }
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        _conn?.Dispose();
        GC.SuppressFinalize(this);
    }

    #endregion
}

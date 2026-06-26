using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
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

    public RedisService(IOptionsMonitor<RedisOption> options, ILogger<RedisService> logger)
        : this(options.CurrentValue, logger)
    {
    }

    public RedisService(RedisOption options, ILogger<RedisService> logger)
    {
        var connectionString = options.ConnectionString;
        _conn = ConnectionMultiplexer.Connect(connectionString);
        _db = _conn.GetDatabase(options.DbNumber);
        LockKey = options.LockKey;
        Expiry = options.Expiry;
        _logger = logger;
    }

    #region Hash

    public async Task<ConcurrentDictionary<string, string>> HashGetAsync(string key)
    {
        return (await _db.HashGetAllAsync(key)).ToConcurrentDictionary();
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
        {
            hs[field.Key] = field.Value;
        }
        await HashSetAsync(key, hs);
    }

    public async Task<bool> HashSetFieldAsync(string key, ConcurrentDictionary<string, string> fields)
    {
        try
        {
            if (fields != null && !fields.IsEmpty)
                await HashSetAsync(key, fields);

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

    private const string AcquireLockScript = @"
        local lockKey = KEYS[1]
        local key = KEYS[2]
        local lockValue = ARGV[1]
        local expiryTimestamp = ARGV[2]
        local currentTime = tonumber(ARGV[3])

        local value = redis.call('HGET', lockKey, key)
        if not value or tonumber(value:match(':(%d+)$')) < currentTime then
            redis.call('HSET', lockKey, key, lockValue .. ':' .. expiryTimestamp)
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
        local newExpiryTimestamp = ARGV[2]
        local currentTime = tonumber(ARGV[3])

        local currentValue = redis.call('HGET', lockKey, key)
        if currentValue then
            local valuePart = currentValue:match('^(.-):')
            if valuePart == lockValue then
                local oldExpiry = tonumber(currentValue:match(':(%d+)$'))
                if oldExpiry and oldExpiry >= currentTime then
                    redis.call('HSET', lockKey, key, lockValue .. ':' .. newExpiryTimestamp)
                    return true
                end
            end
        end
        return false";

    public async Task<bool> AcquireLockAsync(string key, string lockValue)
    {
        try
        {
            var expiry = DateTimeOffset.Now.Add(TimeSpan.FromSeconds(Expiry)).ToUnixTimeMilliseconds().ToString();
            var currentTime = DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString();

            var result = await _db.ScriptEvaluateAsync(AcquireLockScript,
                new RedisKey[] { SlotTag + LockKey, SlotTag + key },
                new RedisValue[] { lockValue, expiry, currentTime });

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

    public async Task<bool> ReleaseLockAsync(string key, string lockValue)
    {
        try
        {
            var result = await _db.ScriptEvaluateAsync(ReleaseLockScript,
                new RedisKey[] { SlotTag + LockKey, SlotTag + key },
                new RedisValue[] { lockValue });

            var isRelease = (bool)result;
            if (!isRelease)
                _logger.LogWarning("Failed to release lock for key {Key} (lock may have expired or been held by another)", key);

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
                var currentTime = DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString();
                var newExpiryTimestamp = DateTimeOffset.Now.Add(TimeSpan.FromSeconds(Expiry)).ToUnixTimeMilliseconds().ToString();

                var result = await _db.ScriptEvaluateAsync(RenewalLockScript,
                    new RedisKey[] { SlotTag + LockKey, SlotTag + key },
                    new RedisValue[] { lockValue, newExpiryTimestamp, currentTime });

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

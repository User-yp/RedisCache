using System.Collections.Concurrent;

namespace RedisCache.RedisSever;

public interface IRedisService
{
    Task<ConcurrentDictionary<string, string>> HashGetAsync(string key);
    Task<ConcurrentDictionary<string, string>> HashGetFieldsAsync(string key, IEnumerable<string> fields);
    Task<ConcurrentDictionary<string, string>> HashGetFieldAsync(string key, string fields);
    Task HashSetAsync(string key, ConcurrentDictionary<string, string> entries);
    Task HashSetFieldsAsync(string key, ConcurrentDictionary<string, string> fields);
    Task<bool> HashSetFieldAsync(string key, ConcurrentDictionary<string, string> fields);
    Task<bool> HashFieldsExistsAsync(string key, IEnumerable<string> fields);
    Task<long> HashDeleteFieldsAsync(string key, IEnumerable<string> fields);
    Task<long> GetHashLength(string key);
    Task<bool> KeyExistsAsync(string key);
    Task<bool> AcquireLockAsync(string key, string lockValue);
    Task<bool> AcquireLockWithRetryAsync(string key, string lockValue, int maxRetries = 3, int baseDelayMs = 100);
    Task<bool> ReleaseLockAsync(string key, string lockValue);
    Task RenewalLockAsync(string key, string lockValue, CancellationToken cancellationToken);
}

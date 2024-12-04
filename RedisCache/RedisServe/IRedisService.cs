using System.Collections.Concurrent;

namespace RedisCache.RedisServe;

public interface IRedisService
{
    Task<bool> StringSetAsync<T>(string key, T value);
    Task<ConcurrentDictionary<string, string>> HashGetAsync(string key);
    Task<ConcurrentDictionary<string, string>> HashGetFieldsAsync(string key, IEnumerable<string> fields);
    Task HashSetAsync(string key, ConcurrentDictionary<string, string> entries);
    Task HashSetFieldsAsync(string key, ConcurrentDictionary<string, string> fields);
    Task HashSetorCreateFieldsAsync(string key, ConcurrentDictionary<string, string> fields);
    Task<bool> HashFieldsExistsAsync(string key, IEnumerable<string> fields);
    Task<bool> HashDeleteAsync(string key);
    Task<bool> HashDeleteFieldsAsync(string key, IEnumerable<string> fields);
    Task<long> GetHashLength(string key);
   /* IEnumerable<string> GetAllKeys();
    IEnumerable<string> GetAllKeys(EndPoint endPoint);*/
    Task<bool> KeyExpireAsync(string key, TimeSpan? expiry);
    Task<bool> KeyExistsAsync(string key);
    Task<long> KeyDeleteAsync(IEnumerable<string> keys);
    Task<bool> KeyDeleteAsync(string keys);

}

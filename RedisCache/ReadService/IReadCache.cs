using RedisCache.DBService;

namespace RedisCache.ReadService;

public interface IReadCache
{
    Task<T?> GetAsync<T>(string key, string fieldKey) where T : RootEntity;
    Task SetKeyAsync<T>(string key) where T : RootEntity;
}

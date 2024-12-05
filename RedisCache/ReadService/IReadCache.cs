namespace RedisCache.ReadService;

public interface IReadCache
{
    Task<T?> GetAsync<T>(string key, string fieldKey) where T : class;
}

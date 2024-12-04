namespace RedisCache.ReadService;

public interface IReadCache
{
    Task<string?> GetAsync<T>(string key, string fieldKey) where T : class;
}

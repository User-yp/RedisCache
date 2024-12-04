namespace RedisCache.WriteService;

public interface IWriteCache
{
    Task AddRedisAsync<T>(T value) where T : class;
    Task AddRedisAsync<T>(List<T> values) where T : class;
}

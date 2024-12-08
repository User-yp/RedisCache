using RedisCache.DBService;

namespace RedisCache.WriteService;

public interface IWriteCache
{
    Task<T?> GetOneAsync<T>(string key, string redisKey) where T : RootEntity;
    Task AddRedisAsync<T>(T value) where T : RootEntity;
    Task AddRedisAsync<T>(List<T> values) where T : RootEntity;
}

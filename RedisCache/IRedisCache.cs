namespace RedisCache;

public interface IRedisCache
{
    /// <summary>
    /// 异步写入单个实体到 Redis 缓冲区
    /// </summary>
    Task<bool> PostRedisAsync(object value);

    /// <summary>
    /// 异步批量写入实体到 Redis 缓冲区
    /// </summary>
    Task<bool> PostRedisAsync(List<object> values);
}

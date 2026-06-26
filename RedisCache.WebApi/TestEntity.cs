using RedisCache.Attributes;

namespace RedisCache.WebApi;

[RedisEntity]
public class TestEntity
{
    [RedisKey]
    public Guid Id { get; protected set; } = Guid.NewGuid();

    /// <summary>
    /// 预计算的 Redis Key（缓存 GetRedisKey() 结果，避免重复反射）
    /// </summary>
    public string EntityKey { get; protected set; }

    public string Name { get; set; }
    public string Description { get; set; }
    public string Type { get; set; }

    public TestEntity(string name, string description, string type)
    {
        Name = name;
        Description = description;
        Type = type;
        EntityKey = this.GetRedisKey();
    }
}

using RedisCache.Attributes;
using RedisCache.DBService;

namespace RedisCache.Domain.Entity;
[RedisEntity]
public class TestEntity : RootEntity
{
    [RedisKey]
    public string Name { get; set; }
    [RedisKey]
    public string Description { get; set; }
    public string Type { get; set; }
    public TestEntity(string Name, string Description, string Type)
    {
        this.Name = Name;
        this.Description = Description;
        this.Type = Type;
        RedisKey = this.GetRedisKey();
    }
}

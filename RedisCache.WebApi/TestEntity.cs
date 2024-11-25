using RedisCache.Attributes;

namespace RedisCache.WebApi;
[RedisEntity]
public class TestEntity
{
    [RedisKey]
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string Type { get; set; }
}

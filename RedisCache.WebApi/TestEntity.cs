using RedisCache.Attributes;
using RedisCache.DBService;

namespace RedisCache.WebApi;
[RedisEntity]
public class TestEntity: AggregateRootEntity
{
    [RedisKey]
    public string Name { get; set; }
    public string Description { get; set; }
    public string Type { get; set; }
    public override void SetRedisKey()
    {
        base.SetRedisKey();
    }
}

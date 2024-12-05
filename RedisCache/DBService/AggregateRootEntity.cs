using RedisCache.Attributes;

namespace RedisCache.DBService;

public class AggregateRootEntity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();
    public string RedisKey { get; protected set; }
    public virtual void SetRedisKey()
    {
        RedisKey=this.GetRedisKey();
    }
}
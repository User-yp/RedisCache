using RedisCache.Attributes;

namespace RedisCache.DBService;

public class RootEntity
{
    [RedisKey]
    public Guid Id { get; protected set; } = Guid.NewGuid();
    public string RedisKey { get; protected set; }
}
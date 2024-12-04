using Microsoft.Extensions.Options;
using RedisCache.Options;
using RedisCache.RedisServe;
using StackExchange.Redis;

namespace RedisCache.Middel;

public class ReadRedis : RedisService, IReadRedis
{
    public ReadRedis(IOptionsMonitor<ReadCacheOption> options, Func<int, IDatabase> getDatabase)
        : this(options.CurrentValue.ReadDbNumber, getDatabase)
    {
    }

    public ReadRedis(int dbNumber, Func<int, IDatabase> getDatabase)
        : base(getDatabase(dbNumber)) // 使用传入的 Func 来获取 IDatabase  
    {
    }
}

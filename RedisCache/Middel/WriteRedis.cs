using Microsoft.Extensions.Options;
using RedisCache.Options;
using RedisCache.RedisServe;
using StackExchange.Redis;

namespace RedisCache.Middel;
public class WriteRedis : RedisService, IWriteRedis
{
    public WriteRedis(IOptionsMonitor<WriteCacheOption> options, Func<int, IDatabase> getDatabase)
        : this(options.CurrentValue.WriteDbNumber, getDatabase)
    {
    }

    public WriteRedis(int dbNumber, Func<int, IDatabase> getDatabase)
        : base(getDatabase(dbNumber)) // 使用传入的 Func 来获取 IDatabase  
    {
    }
}

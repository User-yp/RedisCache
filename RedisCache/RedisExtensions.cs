using Microsoft.Extensions.DependencyInjection;
using RedisCache.DbService;
using RedisCache.DBService;
using RedisCache.MediatREvent;
using RedisCache.Middel;
using RedisCache.Options;
using RedisCache.ReadService;
using RedisCache.RedisServe;
using RedisCache.WriteService;
using StackExchange.Redis;

namespace RedisCache;

public static class RedisExtensions
{
    public static IServiceCollection AddRedisService(this IServiceCollection service, string connStr, int writeDbNumber, 
        int threhold, bool isPolling, int interval,int readDbNumber, int capacity, int expiryTime)
    {
        // 注册ConnectionMultiplexer为单例 
        service.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(connStr));
        service.AddScoped<Func<int, IDatabase>>(provider =>
        {
            var multiplexer = provider.GetRequiredService<IConnectionMultiplexer>();
            return dbNum => multiplexer.GetDatabase(dbNum); // 返回一个方法，用于获取指定数据库  
        });
        // 注册IDatabase实例  
        service.AddScoped(provider => provider.GetRequiredService<Func<int, IDatabase>>()(writeDbNumber));
        service.AddScoped(provider => provider.GetRequiredService<Func<int, IDatabase>>()(readDbNumber));

        service.Configure((WriteCacheOption options) =>
        {
            options.WriteDbNumber = writeDbNumber;
            options.Threhold = threhold;
            options.IsPolling = isPolling;
            options.Interval = TimeSpan.FromSeconds(interval);
        });
        service.Configure((ReadCacheOption options) =>
        {
            options.ReadDbNumber = readDbNumber;
            options.Capacity = capacity;
            options.ExpiryTime = expiryTime;
        });
        service.AddMediatR();
        service.AddScoped<IDBContext, DBContext>();
        service.AddScoped<IRedisService, RedisService>();
        service.AddScoped<IReadRedis, ReadRedis>();
        service.AddScoped<IWriteRedis, WriteRedis>();
        service.AddScoped<IReadCache,ReadCache>();
        service.AddScoped<IWriteCache, WriteCache>();

        return service;
    }
}

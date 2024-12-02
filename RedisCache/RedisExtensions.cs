using Microsoft.Extensions.DependencyInjection;
using RedisCache.DbService;
using RedisCache.RedisServe;

namespace RedisCache;

public static class RedisExtensions
{
    public static IServiceCollection AddRedisService(this IServiceCollection service, string connStr, int dbNumber, int threhold, bool isPolling, int interval)
    {
        service.Configure<RedisOptions>(options =>
        {
            options.ConnectionString = connStr;
            options.DbNumber = dbNumber;
            options.Threhold = threhold;
            options.IsPolling = isPolling;
            options.Interval = TimeSpan.FromSeconds(interval);
        });
        service.AddTransient<ICacheContext, CacheContext>();
        service.AddSingleton<IRedisService, RedisService>();
        service.AddScoped<IRedisCache, RedisCache>();

        return service;
    }
}

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedisCache.RedisSever;

namespace RedisCache;

/// <summary>
/// RedisCache 服务注册扩展
/// </summary>
public static class RedisExtensions
{
    /// <summary>
    /// 注册 RedisCache 服务到 DI 容器。
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置（读取 RedisOption 节点）</param>
    /// <param name="onFlush">
    /// 数据刷盘回调 — 当 Redis Hash 达到阈值时触发。
    /// 签名：Task&lt;bool&gt; handler(IServiceProvider sp, string key, List&lt;object&gt; entities)
    /// <br/>- sp：根 IServiceProvider，在回调中用它创建 Scope 解析 Scoped 服务（如 DbContext）
    /// <br/>- key：实体类型名
    /// <br/>- entities：待刷盘的实体列表
    /// <br/>- 返回 true 表示成功（删除 Redis 数据），false 表示失败（保留数据）
    /// <br/>如果不提供此回调，组件仅缓冲写入 Redis，不会自动刷盘。
    /// </param>
    /// <example>
    /// <code>
    /// services.AddRedisCache(configuration, onFlush: async (sp, key, entities) =>
    /// {
    ///     using var scope = sp.CreateScope();
    ///     var db = scope.ServiceProvider.GetRequiredService&lt;MyDbContext&gt;();
    ///     db.Set&lt;MyEntity&gt;().AddRange(entities.Cast&lt;MyEntity&gt;());
    ///     await db.SaveChangesAsync();
    ///     return true;
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddRedisCache(
        this IServiceCollection services,
        IConfiguration configuration,
        RedisFlushHandler? onFlush = null)
    {
        var opt = configuration.GetSection(nameof(RedisOption)).Get<RedisOption>()
            ?? throw new ArgumentNullException(nameof(RedisOption),
                "RedisOption configuration section is null or empty. " +
                "Please add a 'RedisOption' section to your appsettings.json.");

        services.Configure<RedisOption>(options =>
        {
            options.ConnectionString = opt.ConnectionString;
            options.DbNumber = opt.DbNumber;
            options.Threshold = opt.Threshold;
            options.IsPolling = opt.IsPolling;
            options.Interval = opt.Interval;
            options.Expiry = opt.Expiry;
            options.LockKey = opt.LockKey;
        });

        services.TryAddSingleton<IRedisService, RedisService>();

        // 仅当 IRedisCache 尚未注册时添加
        if (!services.Any(d => d.ServiceType == typeof(IRedisCache)))
        {
            services.AddSingleton<IRedisCache, RedisCache>(sp =>
            {
                var options = sp.GetRequiredService<IOptionsMonitor<RedisOption>>();
                var redisService = sp.GetRequiredService<IRedisService>();
                var provider = sp.GetRequiredService<IServiceProvider>();
                var logger = sp.GetRequiredService<ILogger<RedisCache>>();
                return new RedisCache(options, redisService, provider, logger, onFlush);
            });
        }

        return services;
    }
}

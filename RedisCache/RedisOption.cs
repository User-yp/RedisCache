namespace RedisCache;

/// <summary>
/// Redis 缓存配置选项
/// </summary>
public class RedisOption
{
    /// <summary>Redis 连接字符串</summary>
    public string ConnectionString { get; set; } = null!;

    /// <summary>数据库编号</summary>
    public int DbNumber { get; set; }

    /// <summary>写入数据库的阈值（Hash 长度达到此值触发刷盘）</summary>
    public int Threshold { get; set; }

    /// <summary>是否启用轮询</summary>
    public bool IsPolling { get; set; }

    /// <summary>轮询间隔（秒）</summary>
    public int Interval { get; set; }

    /// <summary>分布式锁的 Hash 键名</summary>
    public string LockKey { get; set; } = null!;

    /// <summary>分布式锁过期时间（秒）</summary>
    public int Expiry { get; set; }
}

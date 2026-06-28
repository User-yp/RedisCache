namespace RedisCache.Tests;

/// <summary>
/// 测试用实体 — 模拟用户实体
/// </summary>
[RedisEntity]
public class TestUser
{
    [RedisKey]
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int Age { get; set; }
}

/// <summary>
/// 测试用实体 — 模拟订单实体（复合 RedisKey）
/// </summary>
[RedisEntity]
public class TestOrder
{
    [RedisKey]
    public string OrderId { get; set; } = string.Empty;

    [RedisKey]
    public string UserId { get; set; } = string.Empty;

    public decimal Amount { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// 没有标记 [RedisKey] 的实体 — 用于测试异常路径
/// </summary>
[RedisEntity]
public class InvalidEntity
{
    public string Data { get; set; } = string.Empty;
}

/// <summary>
/// 没有标记 [RedisEntity] 的普通类 — 不应被发现
/// </summary>
public class PlainClass
{
    public string Name { get; set; } = string.Empty;
}

namespace RedisCache.Attributes;

/// <summary>
/// 标记实体类为 Redis 缓存实体（用于轮询发现）
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class RedisEntityAttribute : Attribute
{
}

/// <summary>
/// 标记属性为 Redis Hash 的 field key
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class RedisKeyAttribute : Attribute
{
}

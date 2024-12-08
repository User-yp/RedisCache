namespace RedisCache.Attributes;

[AttributeUsage(AttributeTargets.Property)]
public class RedisKeyAttribute : Attribute
{
    public RedisKeyAttribute() { }
}
[AttributeUsage(AttributeTargets.Class)]
public class RedisEntityAttribute : Attribute
{
    public RedisEntityAttribute() { }
}
[AttributeUsage(AttributeTargets.Class)]
public class RedisDbContextAttribute : Attribute
{
    public RedisDbContextAttribute() { }
}

using Newtonsoft.Json;
using System.Reflection;

namespace RedisCache.Attributes;

/// <summary>
/// 实体 Key 扩展方法
/// </summary>
public static class RedisKeyExtension
{
    private static readonly Lazy<List<Type>> EntityTypes = new(GetEntityTypes);

    /// <summary>
    /// 获取对象的 RedisKey（基于标记了 [RedisKey] 的属性值组合生成 JSON 数组）
    /// </summary>
    public static string GetRedisKey(this object instance)
    {
        var type = instance.GetType();
        var propInfos = type.GetProperties()
            .Where(prop => Attribute.IsDefined(prop, typeof(RedisKeyAttribute)))
            .ToList();

        if (propInfos.Count == 0)
            throw new ArgumentException($"Type '{type.Name}' has no properties attributed with [RedisKey]");

        var redisKey = new List<string>();
        foreach (var propInfo in propInfos)
        {
            var value = propInfo.GetValue(instance)?.ToString() ?? string.Empty;
            redisKey.Add(value);
        }
        return JsonConvert.SerializeObject(redisKey);
    }

    /// <summary>
    /// 根据类型名称获取标记了 [RedisEntity] 的实体类型
    /// </summary>
    public static Type GetRedisEntity(this string tKey)
    {
        return EntityTypes.Value.FirstOrDefault(t => t.Name == tKey)
            ?? throw new ArgumentException($"No class attributed with [RedisEntity] found for type name '{tKey}'");
    }

    /// <summary>
    /// 获取所有 [RedisEntity] 类型名称（用于轮询）
    /// </summary>
    public static List<string> GetEntityKeys()
    {
        return EntityTypes.Value.Select(t => t.Name).Distinct().ToList();
    }

    private static List<Type> GetEntityTypes()
    {
        var ass = Assembly.GetEntryAssembly()
            ?? throw new InvalidOperationException(
                "Entry assembly is null. This may happen in unit test or plugin scenarios.");

        return ass.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract
                && t.GetCustomAttributes(typeof(RedisEntityAttribute), false).Length != 0)
            .ToList();
    }
}

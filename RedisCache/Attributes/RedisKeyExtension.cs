using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Reflection;

namespace RedisCache.Attributes;

/// <summary>
/// 实体 Key 扩展方法 — 内置反射缓存，热路径零分配
/// </summary>
public static class RedisKeyExtension
{
    private static readonly List<Type> ManualEntityTypes = new();
    private static List<Type>? cachedEntityTypes;

    // ==================== 反射缓存（热路径优化） ====================

    /// <summary>
    /// 缓存每个 Type 的 [RedisKey] PropertyInfo 列表，避免每次调用 GetRedisKey 做反射
    /// </summary>
    private static readonly ConcurrentDictionary<Type, CachedKeyAccessor> KeyAccessorCache = new();

    private sealed class CachedKeyAccessor
    {
        public PropertyInfo[] Properties { get; init; } = Array.Empty<PropertyInfo>();
    }

    /// <summary>
    /// 获取或构建类型的 Key 访问器
    /// </summary>
    private static CachedKeyAccessor GetOrBuildAccessor(Type type)
    {
        return KeyAccessorCache.GetOrAdd(type, static t =>
        {
            var props = t.GetProperties()
                .Where(prop => Attribute.IsDefined(prop, typeof(RedisKeyAttribute)))
                .ToArray();

            if (props.Length == 0)
                throw new ArgumentException($"Type '{t.Name}' has no properties attributed with [RedisKey]");

            return new CachedKeyAccessor { Properties = props };
        });
    }

    // ==================== 类型注册 ====================

    private static List<Type> EntityTypes
    {
        get
        {
            if (cachedEntityTypes == null)
                cachedEntityTypes = GetEntityTypes();
            return cachedEntityTypes;
        }
    }

    public static void RegisterEntityTypes(params Type[] types)
    {
        foreach (var type in types)
        {
            if (type.IsClass && !type.IsAbstract
                && type.GetCustomAttributes(typeof(RedisEntityAttribute), false).Length != 0
                && !ManualEntityTypes.Contains(type))
            {
                ManualEntityTypes.Add(type);
            }
        }
        if (ManualEntityTypes.Count > 0)
            cachedEntityTypes = null;
    }

    public static void ClearEntityTypes()
    {
        ManualEntityTypes.Clear();
        cachedEntityTypes = null;
        KeyAccessorCache.Clear();
    }

    // ==================== 公开 API ====================

    /// <summary>
    /// 获取对象的 RedisKey — 使用缓存的 PropertyInfo，避免重复反射
    /// </summary>
    public static string GetRedisKey(this object instance)
    {
        var type = instance.GetType();
        var accessor = GetOrBuildAccessor(type);

        var values = new List<string>(accessor.Properties.Length);
        foreach (var prop in accessor.Properties)
        {
            var value = prop.GetValue(instance)?.ToString() ?? string.Empty;
            values.Add(value);
        }
        return JsonConvert.SerializeObject(values);
    }

    public static Type GetRedisEntity(this string tKey)
    {
        return EntityTypes.FirstOrDefault(t => t.Name == tKey)
            ?? throw new ArgumentException($"No class attributed with [RedisEntity] found for type name '{tKey}'");
    }

    public static List<string> GetEntityKeys()
    {
        return EntityTypes.Select(t => t.Name).Distinct().ToList();
    }

    // ==================== 内部：程序集扫描 ====================

    private static List<Type> GetEntityTypes()
    {
        var types = new List<Type>();
        types.AddRange(ManualEntityTypes);

        var entryAssembly = Assembly.GetEntryAssembly();
        if (entryAssembly != null)
        {
            try
            {
                var scannedTypes = entryAssembly.GetTypes()
                    .Where(t => t.IsClass && !t.IsAbstract
                        && t.GetCustomAttributes(typeof(RedisEntityAttribute), false).Length != 0);

                foreach (var t in scannedTypes)
                {
                    if (!types.Contains(t))
                        types.Add(t);
                }
            }
            catch (ReflectionTypeLoadException) { }
        }

        return types;
    }
}

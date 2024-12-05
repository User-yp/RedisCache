using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Reflection;

namespace RedisCache.Attributes;

public static class AttributesExtension
{
    private static readonly List<Type> entityTypes = GetEntityType();
    private static readonly List<Type> dbContextTypes = GetDbContextType();

    public static string GetRedisKey<T>(this T instance)
    {
        var type = typeof(T);
        var propInfos = type.GetProperties()
            .Where(prop => Attribute.IsDefined(prop, typeof(RedisKeyAttribute)))
            .ToList();

        if (propInfos.Count == 0)
            throw new ArgumentException($"type of '{type}' has no Properties Attributed '[RedisKey]'");

        var redisKey = new List<string>();

        foreach (var propInfo in propInfos)
        {
            var value = propInfo.GetValue(instance)?.ToString();
            redisKey.Add(value);
        }
        return JsonConvert.SerializeObject(redisKey);
    }
    public static List<string> GetRedisKeys<T>(this List<T> instances)
    {
        var type = typeof(T);
        var propInfos = type.GetProperties()
            .Where(prop => Attribute.IsDefined(prop, typeof(RedisKeyAttribute)))
            .ToList();

        if (propInfos.Count == 0)
            throw new ArgumentException($"type of '{type}' has no Properties Attributed '[RedisKey]'");

        var redisKeys = new List<string>();

        foreach (var instance in instances)
        {
            var redisKey = new List<string>();
            foreach (var propInfo in propInfos)
            {
                var value = propInfo.GetValue(instance)?.ToString();
                redisKey.Add(value);
            }
            redisKeys.Add(JsonConvert.SerializeObject(redisKey));
        }
        return redisKeys;
    }
    public static Type GetRedisEntity(this string tKey)
    {
        var enrityType = entityTypes.FirstOrDefault(t => t.Name == tKey)
            ?? throw new ArgumentException($"type of '{tKey}' has no Properties Attributed '[RedisEntity]'");
        return enrityType;
    }

    //从实例对象获取DbSet
    public static (Type, PropertyInfo) GetRedisDbSet<T>(this T instance)
    {
        var type = typeof(T);
        if (type == typeof(string))
            type = entityTypes.FirstOrDefault(t => t.Name == instance?.ToString());
        var dbContextType = dbContextTypes.FirstOrDefault(t => t.GetProperties()
                .Any(t => t.PropertyType == typeof(DbSet<>).MakeGenericType(type)))
            ?? throw new ArgumentException($" has no DBContext Attributed '[RedisDbSet]'");

        var dbSetType = dbContextType.GetProperties().FirstOrDefault(p => p.PropertyType == typeof(DbSet<>).MakeGenericType(type))
            ?? throw new ArgumentException($" There is no {type} type DbSet<{type}>");
        return (dbContextType, dbSetType);
    }
    public static List<string> GetEntityKeys()
    {
        var keys = new List<string>();
        foreach (var entityType in entityTypes)
        {
            if (!keys.Contains(entityType.Name))
                keys.Add(entityType.Name);
        }
        return keys;
    }
    private static List<Type> GetEntityType()
    {
        var assemblies = AssemblyHelper.GetAllReferencedAssemblies().ToList();

        var entityTypes = assemblies.SelectMany(a => a.GetTypes())
                .Where(t => t.IsClass && !t.IsAbstract
                && t.GetCustomAttributes(typeof(RedisEntityAttribute), false).Length != 0)
                .ToList();
        return entityTypes;
    }
    private static List<Type> GetDbContextType()
    {
        var assemblies = AssemblyHelper.GetAllReferencedAssemblies().ToList();

        var dbContextTypes = assemblies.SelectMany(a => a.GetTypes())
                .Where(t => t.IsClass && !t.IsAbstract
                && t.GetCustomAttributes(typeof(RedisDbContextAttribute), false).Length != 0)
                .ToList();
        return dbContextTypes;
    }
}

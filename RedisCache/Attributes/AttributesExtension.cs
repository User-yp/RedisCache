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
        Type type = typeof(T);
        var propInfos = type.GetProperties()
            .Where(prop => Attribute.IsDefined(prop, typeof(RedisKeyAttribute)))
            .ToList();

        if (propInfos.Count == 0)
            throw new ArgumentException($"type of '{type}' has no Properties Attributed '[RedisKey]'");

        List<string> redisKey = new List<string>();

        foreach (var propInfo in propInfos)
        {
            var value = propInfo.GetValue(instance)?.ToString();
            redisKey.Add(value);
        }
        return JsonConvert.SerializeObject(redisKey);
    }

    public static Type GetRedisEntity(this string tKey)
    {
        var enrityType = entityTypes.FirstOrDefault(t => t.Name == tKey)
            ?? throw new ArgumentException($"type of '{tKey}' has no Properties Attributed '[RedisEntity]'");
        return enrityType;

        /*var enrityType = entityTypes.FirstOrDefault(t => t.Name == tKey);
        if (enrityType != null)
            return enrityType;
        var type = assemblies.SelectMany(a=>a.GetTypes()) 
                .Where(t => t.IsClass && !t.IsAbstract
                && t.GetCustomAttributes(typeof(RedisEntityAttribute), false).Length != 0)
                .FirstOrDefault(t => t.Name == tKey);
        if (type == null)
            throw new ArgumentException($"type of '{type}' has no Properties Attributed '[RedisEntity]'");
        entityTypes.Add(type);
        return type;*/
    }

    public static (Type, PropertyInfo) GetRedisDbSet<T>(this T instance)
    {
        Type type = typeof(T);
        var dbContextType = dbContextTypes.FirstOrDefault(t => t.GetProperties()
                .Any(t => t.PropertyType == typeof(DbSet<>).MakeGenericType(type)))
            ?? throw new ArgumentException($" has no DBContext Attributed '[RedisDbSet]'");

        var dbSetType = dbContextType.GetProperties().FirstOrDefault(p => p.PropertyType == typeof(DbSet<>).MakeGenericType(type))
            ?? throw new ArgumentException($" There is no {type} type DbSet<{type}>");
        return (dbContextType, dbSetType);

        /*Type type = typeof(T);
        var types = assemblies.SelectMany(a => a.GetTypes())
                .Where(t => t.IsClass && !t.IsAbstract
                && t.GetCustomAttributes(typeof(RedisDbContextAttribute), false).Length != 0)
                .FirstOrDefault(t => t.GetProperties()
                .Any(t => t.PropertyType == typeof(DbSet<>).MakeGenericType(type)));
        var propType = types.GetProperties().FirstOrDefault(p => p.PropertyType == typeof(DbSet<>).MakeGenericType(type));
        return (types, propType);*/
    }
    public static List<string> GetEntityKeys()
    {
        List<string> keys = new List<string>();
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

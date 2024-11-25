using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Reflection;

namespace RedisCache.Attributes;

public static class AttributesExtension
{
    private static readonly List<Type> entityTypes = new List<Type> { };
    private static readonly List<Assembly> assemblies = new List<Assembly> { };
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
        var enrityType = entityTypes.FirstOrDefault(t => t.Name == tKey);

        /*var typea = new Assembly[] { Assembly.GetExecutingAssembly() }
                    .SelectMany(a => a.GetTypes())
        .Where(t => t.IsClass && !t.IsAbstract
                    && t.GetCustomAttributes(typeof(RedisEntityAttribute), false).Length != 0)
        .FirstOrDefault(t=>t.Name== tKey);*/

        if (enrityType != null)
            return enrityType;

        var ass = Assembly.GetEntryAssembly();
        var ass1 = Assembly.GetAssembly(typeof(string));
        var ass2 = Assembly.GetCallingAssembly();
        var ass3 = Assembly.GetExecutingAssembly();


        var type = ass.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract
                && t.GetCustomAttributes(typeof(RedisEntityAttribute), false).Length != 0)
                .FirstOrDefault(t => t.Name == tKey);

        if (type == null)
            throw new ArgumentException($"type of '{type}' has no Properties Attributed '[RedisEntity]'");

        entityTypes.Add(type);
        return type;
    }

    public static (Type, PropertyInfo) GetRedisDbSet<T>(this T instance, Assembly assembly)
    {
        Type type = typeof(T);

        var ass = Assembly.GetEntryAssembly();
        var ass1 = Assembly.GetExecutingAssembly();
        var ass2 = Assembly.GetCallingAssembly();
        var ass3 = Assembly.GetExecutingAssembly();


        var types = assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract
                && t.GetCustomAttributes(typeof(RedisDbContextAttribute), false).Length != 0)
                .FirstOrDefault(t => t.GetProperties()
                .Any(t => t.PropertyType == typeof(DbSet<>).MakeGenericType(type)));
        var propType = types.GetProperties().FirstOrDefault(p => p.PropertyType == typeof(DbSet<>).MakeGenericType(type));

        return (types, propType);

        /*var types = assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract
                && t.GetCustomAttributes(typeof(RedisDbContextAttribute), false).Length != 0)
                .Where(t=>t.GetProperties()
                .Any(t=>t.PropertyType== typeof(DbSet<>).MakeGenericType(type)))
                .ToList();
        if (types.Count != 0)
            return types[0];*//*

        return null;*/
    }
}

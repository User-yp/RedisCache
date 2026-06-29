using Newtonsoft.Json;
using StackExchange.Redis;
using System.Collections.Concurrent;

namespace RedisCache.RedisSever;

public static class RedisServiceExtension
{
    public static RedisValue ToRedisValue<T>(this T value)
    {
        if (value == null)
            return RedisValue.Null;

        return value switch
        {
            ValueType => value.ToString(),
            string s => s,
            _ => JsonConvert.SerializeObject(value)
        };
    }

    public static RedisValue[] ToRedisValues<T>(this IEnumerable<T> values)
    {
        var arr = values as T[] ?? values.ToArray();
        if (arr.Length == 0) return Array.Empty<RedisValue>();
        var result = new RedisValue[arr.Length];
        for (int i = 0; i < arr.Length; i++)
            result[i] = arr[i].ToRedisValue();
        return result;
    }

    public static HashEntry[] ToHashEntries(this ConcurrentDictionary<string, string> entries)
    {
        if (entries == null || entries.IsEmpty)
            return Array.Empty<HashEntry>();

        var es = new HashEntry[entries.Count];
        var i = 0;
        foreach (var kvp in entries)
        {
            es[i++] = new HashEntry(kvp.Key, kvp.Value);
        }

        return es;
    }

    public static ConcurrentDictionary<string, string> ToConcurrentDictionary(this IEnumerable<HashEntry> entries)
    {
        var hashEntries = entries as HashEntry[] ?? entries.ToArray();
        if (hashEntries.Length == 0)
            return new ConcurrentDictionary<string, string>();

        var dict = new ConcurrentDictionary<string, string>();
        foreach (var entry in hashEntries)
            dict[entry.Name!] = entry.Value!;

        return dict;
    }
    public static ConcurrentDictionary<string, string> ToConcurrentDictionary(this RedisValue hashValues,
       string fields)
    {
        var dict = new ConcurrentDictionary<string, string>();
        if (!hashValues.IsNull)
            dict[fields] = hashValues!;
        return dict;
    }
    public static ConcurrentDictionary<string, string> ToConcurrentDictionary(this RedisValue[] hashValues,
        IEnumerable<string> fields)
    {
        var fieldsArray = fields as string[] ?? fields.ToArray();
        if (hashValues == null || hashValues.Length == 0 || fieldsArray.Length == 0)
            return new ConcurrentDictionary<string, string>();

        var dict = new ConcurrentDictionary<string, string>();
        for (var i = 0; i < fieldsArray.Length; i++)
            dict[fieldsArray[i]] = hashValues[i]!;

        return dict;
    }
}

using Newtonsoft.Json;
using StackExchange.Redis;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RedisCache.RedisSever;

public static class RedisServiceExtension
{
    public static void AddKey(this List<string> strings, string key)
    {
        if (!strings.Contains(key))
        {
            strings.Add(key);
        }
    }

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
        var enumerable = values as T[] ?? values.ToArray();
        return enumerable.Length == 0 ? Array.Empty<RedisValue>() : enumerable.Select(v => v.ToRedisValue()).ToArray();
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

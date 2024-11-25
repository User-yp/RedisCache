using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System.Collections.Concurrent;
using System.Net;
using IDatabase = StackExchange.Redis.IDatabase;

namespace RedisCache.RedisServe;

public class RedisService : IRedisService
{
    private readonly ConnectionMultiplexer _conn;
    private readonly IDatabase _db;

    public RedisService(IOptionsMonitor<RedisOptions> options) : this(options.CurrentValue)
    {
    }

    public RedisService(RedisOptions options)
    {
        var connectionString = options.ConnectionString;
        _conn = ConnectionMultiplexer.Connect(connectionString);

        var dbNumber = options.DbNumber;
        _db = _conn.GetDatabase(dbNumber);
    }


    public async Task<bool> StringSetAsync<T>(string key, T value)
    {
        return await _db.StringSetAsync(key, value.ToRedisValue());
    }

    #region Hash

    public async Task<ConcurrentDictionary<string, string>> HashGetAsync(string key)
    {
        return (await _db.HashGetAllAsync(key)).ToConcurrentDictionary();
    }


    public async Task<ConcurrentDictionary<string, string>> HashGetFieldsAsync(string key, IEnumerable<string> fields)
    {
        return (await _db.HashGetAsync(key, fields.ToRedisValues())).ToConcurrentDictionary(fields);
    }

    public async Task HashSetAsync(string key, ConcurrentDictionary<string, string> entries)
    {
        var val = entries.ToHashEntries();
        if (val != null)
            await _db.HashSetAsync(key, val);
    }


    public async Task HashSetFieldsAsync(string key, ConcurrentDictionary<string, string> fields)
    {
        if (fields == null || fields.IsEmpty)
            return;

        var hs = await HashGetAsync(key);
        foreach (var field in fields)
        {
            //if(!hs.ContainsKey(field.Key))

            //    continue;

            hs[field.Key] = field.Value;
        }
        await HashSetAsync(key, hs);
    }


    public async Task HashSetorCreateFieldsAsync(string key, ConcurrentDictionary<string, string> fields)
    {
        if (!await KeyExistsAsync(key))
            await HashSetAsync(key, fields);
        else
        {
            if (fields == null || fields.IsEmpty)
                return;

            var hs = await HashGetAsync(key);
            foreach (var field in fields)
            {
                //if(!hs.ContainsKey(field.Key))

                //    continue;

                hs[field.Key] = field.Value;
            }
            await HashSetAsync(key, hs);
        }
    }

    public async Task<bool> HashFieldsExistsAsync(string key, IEnumerable<string> fields)
    {
        if (!await KeyExistsAsync(key))
            return false;
        var dic = await HashGetFieldsAsync(key, fields);
        foreach (var field in fields)
        {
            if (dic[field] == null)
                return false;
        }
        return true;
    }
    public async Task<bool> HashDeleteAsync(string key)
    {
        return await KeyDeleteAsync(new string[] { key }) > 0;
    }
    public async Task<bool> HashDeleteFieldsAsync(string key, IEnumerable<string> fields)
    {
        if (fields == null || !fields.Any())
            return false;

        var success = true;
        foreach (var field in fields)
        {
            if (!await _db.HashDeleteAsync(key, field))
                success = false;
        }
        return success;
    }

    #endregion

    #region Key

    public async Task<long> GetHashLength(string key)
    {
        if (!await KeyExistsAsync(key))
            return 0;
        return await _db.HashLengthAsync(key);
    }
    public IEnumerable<string> GetAllKeys()
    {
        return _conn.GetEndPoints().Select(endPoint => _conn.GetServer(endPoint))
            .SelectMany(server => server.Keys().ToStrings());
    }


    public IEnumerable<string> GetAllKeys(EndPoint endPoint)
    {
        return _conn.GetServer(endPoint).Keys().ToStrings();
    }


    public async Task<bool> KeyExistsAsync(string key)
    {
        return await _db.KeyExistsAsync(key);
    }


    public async Task<long> KeyDeleteAsync(IEnumerable<string> keys)
    {
        return await _db.KeyDeleteAsync(keys.Select(k => (RedisKey)k).ToArray());
    }


    public async Task<bool> KeyDeleteAsync(string key)
    {
        return await _db.KeyDeleteAsync(key);
    }
    #endregion

}

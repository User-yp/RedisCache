using StackExchange.Redis;
using System.Collections.Concurrent;
using IDatabase = StackExchange.Redis.IDatabase;

namespace RedisCache.RedisServe;

public class RedisService : IRedisService
{
    //private readonly ConnectionMultiplexer _conn;
    private readonly IDatabase database;
    /*private readonly IDatabase _readDb;
    private readonly IDatabase database;*/

    public RedisService(IDatabase database)
    {
        this.database = database;
    }
    /*public RedisService(IOptionsMonitor<WriteCacheOption> options) : this(options.CurrentValue)
    {
    }

    public RedisService(WriteCacheOption options)
    {
        var connectionString = options.ConnectionString;
        _conn = ConnectionMultiplexer.Connect(connectionString);

        var writeDbNumber = options.ReadDbNumber;
        database = _conn.GetDatabase(writeDbNumber);

        var readDbNumber = options.ReadDbNumber;
        _readDb =_conn.GetDatabase(readDbNumber);
    }*/



    public async Task<bool> StringSetAsync<T>(string key, T value)
    {
        return await database.StringSetAsync(key, value.ToRedisValue());
    }

    #region Hash

    public async Task<ConcurrentDictionary<string, string>> HashGetAsync(string key)
    {
        return (await database.HashGetAllAsync(key)).ToConcurrentDictionary();
    }


    public async Task<ConcurrentDictionary<string, string>> HashGetFieldsAsync(string key, IEnumerable<string> fields)
    {
        return (await database.HashGetAsync(key, fields.ToRedisValues())).ToConcurrentDictionary(fields);
    }

    public async Task HashSetAsync(string key, ConcurrentDictionary<string, string> entries)
    {
        var val = entries.ToHashEntries();
        if (val != null)
            await database.HashSetAsync(key, val);
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
            if (!await database.HashDeleteAsync(key, field))
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
        return await database.HashLengthAsync(key);
    }
    /*public IEnumerable<string> GetAllKeys()
    {
        return _conn.GetEndPoints().Select(endPoint => _conn.GetServer(endPoint))
            .SelectMany(server => server.Keys().ToStrings());
    }


    public IEnumerable<string> GetAllKeys(EndPoint endPoint)
    {
        return _conn.GetServer(endPoint).Keys().ToStrings();
    }*/

    public async Task<bool> KeyExpireAsync(string key, TimeSpan? expiry)
    {
        return await database.KeyExpireAsync(key, expiry);
    }
    public async Task<bool> KeyExistsAsync(string key)
    {
        return await database.KeyExistsAsync(key);
    }


    public async Task<long> KeyDeleteAsync(IEnumerable<string> keys)
    {
        return await database.KeyDeleteAsync(keys.Select(k => (RedisKey)k).ToArray());
    }


    public async Task<bool> KeyDeleteAsync(string key)
    {
        return await database.KeyDeleteAsync(key);
    }
    #endregion

}

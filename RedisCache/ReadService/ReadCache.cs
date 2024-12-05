using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using RedisCache.Attributes;
using RedisCache.DbService;
using RedisCache.Middel;
using RedisCache.Options;
using System.Collections.Concurrent;

namespace RedisCache.ReadService;

public class ReadCache : IReadCache
{
    private readonly int capacity;
    private readonly TimeSpan expiryTime;
    private readonly Dictionary<string, List<string>> cacheMap;
    private readonly LinkedList<string> lruList;
    private readonly ConcurrentDictionary<string, CancellationTokenSource> expiryTokens;
    private readonly IReadRedis readRedis;
    private readonly IDBContext dBContext;

    public ReadCache(IOptionsMonitor<ReadCacheOption> options, IReadRedis readRedis, IDBContext dBContext)
    {
        capacity = options.CurrentValue.Capacity;
        expiryTime = TimeSpan.FromSeconds(options.CurrentValue.ExpiryTime);
        cacheMap = new Dictionary<string, List<string>>(capacity);
        expiryTokens = new ConcurrentDictionary<string, CancellationTokenSource>();
        lruList = new LinkedList<string>();
        this.readRedis = readRedis;
        this.dBContext = dBContext;
    }

    public async Task<T?> GetAsync<T>(string key, string fieldKey) where T : class
    {
        //如果lruList不包含副本key
        if (!lruList.Contains(key))
            return await SetAsync<T>(key, fieldKey);
        //lruList包含副本key todo:若保持lruList与cacheMap同步，则不需要获取TryGetValue返回值
        if (!cacheMap.TryGetValue(key, out var node))
            return null;
        //查找具体实例，若找到
        if (node.Contains(fieldKey))
        {
            //从Redis取数据
            var fieldValue = await readRedis.HashGetFieldsAsync(key, [fieldKey]);
            /*//刷新Redis过期时间
            await readRedis.KeyExpireAsync(key, expiryTime);
            // 移动lruList
            lruList.Remove(key);
            lruList.AddFirst(key);
            //todo: lrulist cacheMap 监听时间*/

            await RefreshExpiry(key);
            return JsonConvert.DeserializeObject<T>(fieldValue[fieldKey]);
        }
        //若内存缓存副本未找到
        else
        {
            //查数据库下当前key对应的数据 todo:加载策略 当前加载所有
            var valueFromDb = await dBContext.GetAsync<T>(key, fieldKey);
            //var keys = valueFromDb.GetRedisKeys();
            //数据库没有这条数据
            if (valueFromDb == null)
                return null;

            await SetOneAsync<T>(key, valueFromDb.GetRedisKey());
            await readRedis.HashSetorCreateFieldsAsync(key, new ConcurrentDictionary<string, string>
            {
                [fieldKey] = JsonConvert.SerializeObject(valueFromDb)
            });
            return valueFromDb;
        }
    }
    //lrulist不包含副本key
    public async Task<T?> SetAsync<T>(string key, string fieldKey) where T : class
    {
        //查数据库,不存在则不做修改
        var value = await dBContext.GetAsync<T>(key, fieldKey);
        if (value == null)
            return null;

        //超出缓存容量
        if (cacheMap.Count >= capacity)
        {
            var lastNode = lruList.Last;
            if (lastNode != null)
            {
                //移除lru表与缓存副本删除redis
                lruList.RemoveLast();
                cacheMap.Remove(lastNode.Value);
                await readRedis.KeyDeleteAsync([lastNode.Value]);
                expiryTokens.TryRemove(lastNode.Value, out _);
            }
        }
        lruList.AddFirst(key);
        var valueFromDb = await dBContext.GetAllAsync<T>(key);
        var keys = new List<string>();

        foreach (var v in valueFromDb)
        {
            var redisKey = v.GetRedisKey();

            await readRedis.HashSetorCreateFieldsAsync(typeof(T).Name, new ConcurrentDictionary<string, string>
            {
                [redisKey] = JsonConvert.SerializeObject(v)
            });
            keys.Add(redisKey);
        }
        cacheMap[key] = keys;
        await RefreshExpiry(key);

        return value;
    }
    //添加单条数据
    public async Task SetOneAsync<T>(string key, string fieldKey) where T : class
    {
        //这里不存在get不到node
        cacheMap.TryGetValue(key, out var node);
        lruList.Remove(key);
        lruList.AddFirst(key);
        node!.Add(fieldKey);

        await RefreshExpiry(key);
    }

    private async Task RefreshExpiry(string key)
    {
        await readRedis.KeyExpireAsync(key, expiryTime);

        // 取消现有的过期任务  
        if (expiryTokens.TryGetValue(key, out var cts))
        {
            cts.Cancel();
        }

        // 创建新的 CancellationTokenSource  
        var newCts = new CancellationTokenSource();
        expiryTokens[key] = newCts;

        // 启动新的过期任务  
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(expiryTime, newCts.Token);
                if (newCts.Token.IsCancellationRequested)
                    return;

                // 过期处理  
                if (cacheMap.TryGetValue(key, out var node))
                {
                    cacheMap.Remove(key);
                    lruList.Remove(key);
                    await readRedis.KeyDeleteAsync(key);
                }
            }
            catch (TaskCanceledException)
            {
                // 任务被取消，什么也不做  
            }
        });
    }
}

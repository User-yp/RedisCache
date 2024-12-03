using Microsoft.Extensions.Options;
using RedisCache.Attributes;
using RedisCache.Options;
using RedisCache.RedisServe;

namespace RedisCache.DbService;

public class LRUCache
{
    private readonly int _capacity;
    private readonly TimeSpan _expiryTime;
    private readonly Dictionary<string, List<string>> _cacheMap;
    private readonly LinkedList<string> _lruList;
    private readonly IRedisService redis;
    private readonly ICacheContext cacheContext;

    public LRUCache(IOptionsMonitor<LRUOptions> options, IRedisService redis, ICacheContext cacheContext)
    {
        _capacity = options.CurrentValue.Capacity;
        _expiryTime = TimeSpan.FromSeconds(options.CurrentValue.ExpiryTime);
        _cacheMap = new Dictionary<string, List<string>>(_capacity);
        _lruList = new LinkedList<string>();
        this.redis = redis;
        this.cacheContext = cacheContext;
    }

    public async Task<string?> GetAsync<T>(string key, string fieldKey) where T : class
    {
        //如果key命中
        if (_cacheMap.TryGetValue(key, out var node))
        {
            //查找具体实例，若找到
            if (node.Contains(fieldKey))
            {
                // 移动到链表头部
                _lruList.Remove(key);
                _lruList.AddFirst(key);
                //刷新Redis过期时间
                await redis.KeyExpireAsync(key, _expiryTime);
                //从Redis获取值
                var fieldValue = await redis.HashGetFieldsAsync(key, [fieldKey]);
                return fieldValue[fieldKey];
            }
            //todo:若未找到，缓存穿透
        }
        else
        {
            var valueFromDb = await cacheContext.GetAllByKeyAsync<T>(key);
            var keys = valueFromDb.GetRedisKey();
            if (keys.Contains(fieldKey))
            {
                await SetAsync<T>(key, keys);
            }
        }
        return null; // 如果没有找到  
    }
    public async Task SetAsync<T>(string key, List<string> keys) where T : class
    {
        if (_cacheMap.TryGetValue(key, out var node))
        {
            // 更新值并移动到链表头部
            node.Clear();
            node.AddRange(keys);
            _lruList.Remove(key);
            _lruList.AddFirst(key);
            await redis.KeyExpireAsync(key, _expiryTime);
        }
        else
        {
            // 如果超出容量，移除最少使用的元素  
            if (_cacheMap.Count >= _capacity)
            {
                var lastNode = _lruList.Last;
                if (lastNode != null)
                {
                    _lruList.RemoveLast();
                    _cacheMap.Remove(lastNode.Value);
                    await redis.KeyDeleteAsync([lastNode.Value]);
                }
            }
            // 添加新项  
            var newNode = new LinkedListNode<string>(key);
            _lruList.AddFirst(newNode);
            _cacheMap[key] = keys;
            var valueFromDb = await cacheContext.GetAllAsync(key.GetRedisEntity());
            Type type = key.GetRedisEntity();
            //await redisCache.AddRedisAsync<type>(valueFromDb);
        }
    }
}

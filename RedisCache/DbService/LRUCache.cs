namespace RedisCache.DbService;

internal class LRUCache
{
    private readonly int _capacity;
    private readonly Dictionary<string, LinkedListNode<CacheItem>> _cacheMap;
    private readonly LinkedList<CacheItem> _lruList;

    public LRUCache(int capacity)
    {
        _capacity = capacity;
        _cacheMap = new Dictionary<string, LinkedListNode<CacheItem>>(capacity);
        _lruList = new LinkedList<CacheItem>();
    }

    public string Get(string key)
    {
        if (_cacheMap.TryGetValue(key, out var node))
        {
            // 移动到链表头部  
            _lruList.Remove(node);
            _lruList.AddFirst(node);
            return node.Value.Value;
        }
        return null; // 如果没有找到  
    }

    public void Set(string key, string value)
    {
        if (_cacheMap.TryGetValue(key, out var node))
        {
            // 更新值并移动到链表头部  
            node.Value.Value = value;
            _lruList.Remove(node);
            _lruList.AddFirst(node);
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
                    _cacheMap.Remove(lastNode.Value.Key);
                }
            }

            // 添加新项  
            var newNode = new LinkedListNode<CacheItem>(new CacheItem(key, value));
            _lruList.AddFirst(newNode);
            _cacheMap[key] = newNode;
        }
    }

    private class CacheItem
    {
        public string Key { get; }
        public string Value { get; set; }

        public CacheItem(string key, string value)
        {
            Key = key;
            Value = value;
        }
    }
}

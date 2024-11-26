using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using RedisCache.Attributes;
using System.Collections.Concurrent;
using System.Collections;
using System.Reflection;
using RedisCache.RedisServe;
using RedisCache.DbService;

namespace RedisCache;

public class RedisCache:IRedisCache
{
    private readonly IRedisService redisService;
    private readonly CacheContext dBContext;
    private static Task pollingTask;
    private readonly int threhold;
    private readonly bool isPolling;
    private static bool isPollingStarted = false;
    private readonly TimeSpan interval;
    private readonly List<string> redisKeys = new List<string>();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new ConcurrentDictionary<string, SemaphoreSlim>();

    public RedisCache(IOptionsMonitor<RedisOptions> options, IRedisService redisService, CacheContext dBContext)
    {

        this.redisService = redisService;
        this.dBContext = dBContext;
        threhold = options.CurrentValue.Threhold;
        isPolling = options.CurrentValue.IsPolling;
        interval = options.CurrentValue.Interval;

        PollingThread();
    }

    private void PollingThread()
    {
        if (!isPolling || isPollingStarted)
            return;

        pollingTask = Task.Run(async () =>
        {
            while (true)
            {
                if (redisKeys.Count != 0)
                {
                    redisKeys.ForEach(async redisKey =>
                    {
                        var semaphore = _locks.GetOrAdd(redisKey, _ => new SemaphoreSlim(1, 1));

                        await semaphore.WaitAsync();
                        try
                        {
                            await WriteDataBase(redisKey);
                        }
                        finally
                        {
                            // Release the semaphore  
                            semaphore.Release();
                        }
                    });
                }
                Console.WriteLine($"线程{Environment.CurrentManagedThreadId}-PollingThread");
                await Task.Delay(interval);
            }
        });
        isPollingStarted = true;
    }

    public async Task AddRedisAsync<T>(T value) where T : class
    {
        redisKeys.AddKey(typeof(T).Name);

        if (!isPolling)
            await WriteDataBase(value);

        var redisKey = value.GetRedisKey();

        await redisService.HashSetorCreateFieldsAsync(typeof(T).Name, new ConcurrentDictionary<string, string>
        {
            [redisKey] = JsonConvert.SerializeObject(value)
        });
        Console.WriteLine($"{Environment.CurrentManagedThreadId}-写入redis-{JsonConvert.SerializeObject(value)}");
    }

    public async Task AddRedisAsync<T>(List<T> values) where T : class
    {
        redisKeys.AddKey(typeof(T).Name);
        if (!isPolling)
            await WriteDataBase(values[0]);

        foreach (var value in values)
        {
            var redisKey = value.GetRedisKey();

            await redisService.HashSetorCreateFieldsAsync(typeof(T).Name, new ConcurrentDictionary<string, string>
            {
                [redisKey] = JsonConvert.SerializeObject(value)
            });
        }
    }
    private async Task<bool> WriteDataBase<T>(T Tkey) where T : class
    {

        if (await GetCountAsync(typeof(T).Name) < threhold)
            return true;

        var values = await redisService.HashGetAsync(typeof(T).Name);

        List<T> listValue = new List<T>();
        List<string> listKey = new List<string>();
        foreach (var value in values)
        {
            var entity = JsonConvert.DeserializeObject<T>(value.Value);
            listValue.Add(entity);
            listKey.Add(value.Key);
        }

        if (!await dBContext.InsertDatabase(listValue))
            return false;
        Console.WriteLine($"线程：{Environment.CurrentManagedThreadId}-写入数据库");

        await redisService.HashDeleteFieldsAsync(typeof(T).Name, listKey.AsEnumerable());
        Console.WriteLine($"线程：{Environment.CurrentManagedThreadId}-删除redis");
        return true;
    }

    private async Task<bool> WriteDataBase(string Tkey)
    {
        if (await GetCountAsync(Tkey) < threhold)
            return true;

        var values = await redisService.HashGetAsync(Tkey);

        Type type = Tkey.GetRedisEntity();

        var listValueType = typeof(List<>).MakeGenericType(type);
        var listValue = (IList)Activator.CreateInstance(listValueType);

        List<string> listKey = new List<string>();

        foreach (var value in values)
        {
            var entity = JsonConvert.DeserializeObject(value.Value, type);
            listValue.Add(entity);
            listKey.Add(value.Key);
        }

        var insertMethod = dBContext.GetType().GetMethod("InsertDatabase").MakeGenericMethod(type);
        var insertTask = (Task)insertMethod.Invoke(dBContext, new[] { listValue });
        await insertTask; // 等待任务完成  

        //bool insertResult = (insertTask as dynamic).Result; // 使用 dynamic 来获取结果  

        if (!(insertTask as dynamic).Result)
            return false;
        Console.WriteLine($"{Environment.CurrentManagedThreadId}-写入数据库");

        await redisService.HashDeleteFieldsAsync(Tkey, listKey.AsEnumerable());
        Console.WriteLine($"{Environment.CurrentManagedThreadId}删除redis");

        return true;
    }
    //计数
    private async Task<long> GetCountAsync(string key)
    {
        return await redisService.GetHashLength(key);
    }

    #region test
    //test
    public async Task<bool> DeletedAsync(string key, string field)
    {
        return await redisService.HashDeleteFieldsAsync(key, new List<string> { field }.AsEnumerable());
    }
    public async Task<bool> BatchInsertAsync()
    {
        /*List<EQP_EVENT> _events = new();
        for (int i = 0; i < 100; i++)
        {
            EQP_EVENT _EVENT = new()
            {
                MODEL_ID = "100_" + i,
                CEID = i.ToString(),
                CEID_DESC = DateTime.Now.ToString()
            };
            _events.Add(_EVENT);
        }
        return await dBContext.InsertDatabase(_events);*/
        return await Task.FromResult(true);
    }
    public async Task<bool> BatchInsert2Async()
    {
        string Tkey = "EQP_EVENT";
        string Tkey1 = "Book";
        string Tkey2 = "Model";
        string Tkey3 = "Order";
        //Type type = Tkey.GetRedisEntity();
        Type type1 = Tkey1.GetRedisEntity();
        Type type2 = Tkey2.GetRedisEntity();
        Type type3 = Tkey3.GetRedisEntity();
        /*List<object> _events = new();
        for (int i = 0; i < 3; i++)
        {
            EQP_EVENT _EVENT = new()
            {
                MODEL_ID = "1-" + i,
                CEID = i.ToString(),
                CEID_DESC = DateTime.Now.ToString()
            };
            _events.Add(_EVENT);
        }
        return await dBContext.InsertDatabase(_events);*/
        return false;
    }

    #endregion
}

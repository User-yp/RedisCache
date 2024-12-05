using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using RedisCache.Attributes;
using System.Collections.Concurrent;
using System.Collections;
using RedisCache.DbService;
using RedisCache.Options;
using RedisCache.Middel;

namespace RedisCache.WriteService;

public class WriteCache : IWriteCache
{
    private readonly IWriteRedis writeRedis;
    private readonly IDBContext dBContext;
    private readonly int threhold;
    private readonly bool isPolling;
    private static bool isPollingStarted = false;
    private readonly TimeSpan interval;
    private readonly List<string> redisKeys = AttributesExtension.GetEntityKeys();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new ConcurrentDictionary<string, SemaphoreSlim>();

    public WriteCache(IOptionsMonitor<WriteCacheOption> options, IWriteRedis writeRedis, IDBContext dBContext)
    {

        this.writeRedis = writeRedis;
        this.dBContext = dBContext;
        threhold = options.CurrentValue.Threhold;
        isPolling = options.CurrentValue.IsPolling;
        interval = options.CurrentValue.Interval;

        PollingThread();
    }

    private void PollingThread()
    {
        if (!isPolling || isPollingStarted || redisKeys.Count == 0)
            return;
        Task.Run(async () =>
        {
            while (true)
            {
                var tasks = new List<Task<(bool isSucess, string key)>>();
                redisKeys.ForEach(async redisKey =>
                {
                    var semaphore = _locks.GetOrAdd(redisKey, _ => new SemaphoreSlim(1, 1));
                    await semaphore.WaitAsync();
                    tasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            return (await WriteDataBase(redisKey), redisKey);
                        }
                        finally
                        {
                            semaphore.Release();// Release the semaphore  
                        }
                    }));
                });
                var res = await Task.WhenAll(tasks);
                await Task.Delay(interval);
                Console.WriteLine($"轮询线程{DateTime.Now}");
            }
        });
        isPollingStarted = true;
    }

    public async Task AddRedisAsync<T>(T value) where T : class
    {
        if (!isPolling)//redisKeys.AddKey(typeof(T).Name);
            await WriteDataBase(value);

        var redisKey = value.GetRedisKey();

        await writeRedis.HashSetorCreateFieldsAsync(typeof(T).Name, new ConcurrentDictionary<string, string>
        {
            [redisKey] = JsonConvert.SerializeObject(value)
        });
        Console.WriteLine($"{Environment.CurrentManagedThreadId}-写入redis-{JsonConvert.SerializeObject(value)}");
    }

    public async Task AddRedisAsync<T>(List<T> values) where T : class
    {
        if (!isPolling)//redisKeys.AddKey(typeof(T).Name);
            await WriteDataBase(values[0]);

        foreach (var value in values)
        {
            var redisKey = value.GetRedisKey();

            await writeRedis.HashSetorCreateFieldsAsync(typeof(T).Name, new ConcurrentDictionary<string, string>
            {
                [redisKey] = JsonConvert.SerializeObject(value)
            });
        }
    }
    private async Task<bool> WriteDataBase<T>(T Tkey) where T : class
    {

        if (await GetCountAsync(typeof(T).Name) < threhold)
            return true;

        var values = await writeRedis.HashGetAsync(typeof(T).Name);

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

        await writeRedis.HashDeleteFieldsAsync(typeof(T).Name, listKey.AsEnumerable());
        Console.WriteLine($"线程：{Environment.CurrentManagedThreadId}-删除redis");
        return true;
    }

    private async Task<bool> WriteDataBase(string Tkey)
    {
        if (await GetCountAsync(Tkey) < threhold)
            return true;

        var values = await writeRedis.HashGetAsync(Tkey);

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

        var insertMethod = dBContext.GetType().GetMethod("InsertDatabase")?.MakeGenericMethod(type);
        var insertTask = (Task)insertMethod.Invoke(dBContext, [listValue]);
        await insertTask; // 等待任务完成  

        //bool insertResult = (insertTask as dynamic).Result; // 使用 dynamic 来获取结果  

        if (!(insertTask as dynamic).Result)
            return false;
        Console.WriteLine($"{Environment.CurrentManagedThreadId}-写入数据库");

        await writeRedis.HashDeleteFieldsAsync(Tkey, listKey.AsEnumerable());
        Console.WriteLine($"{Environment.CurrentManagedThreadId}删除redis");

        return true;
    }
    //计数
    private async Task<long> GetCountAsync(string key)
    {
        return await writeRedis.GetHashLength(key);
    }
}

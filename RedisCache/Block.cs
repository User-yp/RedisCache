using System.Collections.Concurrent;
using System.Threading.Tasks.Dataflow;

namespace RedisCache;

/// <summary>
/// 数据刷盘委托 — 当 Redis Hash 达到阈值时触发。
/// </summary>
/// <param name="serviceProvider">应用的根 IServiceProvider，可用于创建 Scope 解析 Scoped 服务（如 DbContext）</param>
/// <param name="key">Redis Hash key（对应实体类型名）</param>
/// <param name="entities">待写入数据库的实体列表</param>
/// <returns>true 表示刷盘成功（将删除 Redis 数据）；false 表示失败（保留数据等待重试）</returns>
public delegate Task<bool> RedisFlushHandler(IServiceProvider serviceProvider, string key, List<object> entities);

/// <summary>
/// Redis + Dataflow 缓冲写入调度器
/// </summary>
internal class Block
{
    public Func<object, Task> WriteRedisFunc { get; }
    public Func<string, Task<bool>> WriteDataBaseFunc { get; }
    public ConcurrentDictionary<string, ActionBlock<object>> ActionBlocks { get; }
    public ActionBlock<object> RetryBlock { get; }
    public ConcurrentDictionary<string, int> RetryCount { get; }
    public ConcurrentDictionary<string, SemaphoreSlim> Locks { get; }

    public Block(Func<object, Task> writeRedisAsync, Func<string, Task<bool>> writeDataBaseAsync)
    {
        WriteRedisFunc = writeRedisAsync;
        WriteDataBaseFunc = writeDataBaseAsync;

        RetryBlock = new ActionBlock<object>(async value =>
        {
            await WriteRedisFunc(value);
            await Task.Delay(500);
        }, new ExecutionDataflowBlockOptions
        {
            MaxDegreeOfParallelism = 5
        });

        ActionBlocks = new ConcurrentDictionary<string, ActionBlock<object>>();
        RetryCount = new ConcurrentDictionary<string, int>();
        Locks = new ConcurrentDictionary<string, SemaphoreSlim>();
    }

    public ActionBlock<object> GetBlock(string key)
    {
        return ActionBlocks.GetOrAdd(key, _ =>
        {
            return new ActionBlock<object>(WriteRedisFunc, new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = 20,
                BoundedCapacity = DataflowBlockOptions.Unbounded
            });
        });
    }

    public async Task DoPollingAsync(string redisKey)
    {
        var semaphore = Locks.GetOrAdd(redisKey, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync();
        try
        {
            await WriteDataBaseFunc(redisKey);
        }
        finally
        {
            semaphore.Release();
        }
    }

    public int GetRetryCount(string key)
    {
        return RetryCount.GetOrAdd(key, _ => 0);
    }

    public async Task ResendAsync(string key, object value)
    {
        await RetryBlock.SendAsync(value);
        RetryCount.AddOrUpdate(key, 1, (_, count) => count + 1);
    }

    public int TryRemove(string key)
    {
        return RetryCount.TryRemove(key, out var count) ? count : -1;
    }
}

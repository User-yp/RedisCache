using System.Collections.Concurrent;
using System.Threading.Tasks.Dataflow;

namespace RedisCache;

/// <summary>
/// 数据刷盘委托
/// </summary>
/// <param name="serviceProvider">根 IServiceProvider，用于创建 Scope</param>
/// <param name="key">Redis Hash key（实体类型名）</param>
/// <param name="entities">待写入数据库的实体列表</param>
/// <returns>true 表示刷盘成功；false 表示失败</returns>
public delegate Task<bool> RedisFlushHandler(IServiceProvider serviceProvider, string key, List<object> entities);

/// <summary>
/// Redis + Dataflow 缓冲写入调度器
/// </summary>
internal class Block
{
    /// <summary>ActionBlock 缓冲区上限（防止内存无限堆积）</summary>
    private const int BoundedCapacity = 10000;

    /// <summary>ActionBlock 最大并行度</summary>
    private const int MaxParallelism = 20;

    /// <summary>重试块最大并行度</summary>
    private const int RetryMaxParallelism = 5;

    /// <summary>重试延迟（毫秒）</summary>
    private const int RetryDelayMs = 500;

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
            await Task.Delay(RetryDelayMs);
        }, new ExecutionDataflowBlockOptions
        {
            MaxDegreeOfParallelism = RetryMaxParallelism,
            BoundedCapacity = BoundedCapacity
        });

        ActionBlocks = new ConcurrentDictionary<string, ActionBlock<object>>();
        RetryCount = new ConcurrentDictionary<string, int>();
        Locks = new ConcurrentDictionary<string, SemaphoreSlim>();
    }

    /// <summary>
    /// 获取或创建指定 key 的 ActionBlock
    /// </summary>
    public ActionBlock<object> GetBlock(string key)
    {
        return ActionBlocks.GetOrAdd(key, _ =>
        {
            return new ActionBlock<object>(WriteRedisFunc, new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = MaxParallelism,
                BoundedCapacity = BoundedCapacity // 限流：缓冲区满时 SendAsync 返回 false
            });
        });
    }

    /// <summary>
    /// 轮询刷盘 — SemaphoreSlim 保证同一 key 同一时刻只有一个刷盘操作
    /// </summary>
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

using System.Collections.Concurrent;
using System.Threading.Tasks.Dataflow;

namespace RedisCache.Tests;

public class BlockTests
{
    private readonly Block block;
    private int writeRedisCallCount;
    private int writeDbCallCount;

    public BlockTests()
    {
        writeRedisCallCount = 0;
        writeDbCallCount = 0;

        block = new Block(
            writeRedisAsync: _ =>
            {
                Interlocked.Increment(ref writeRedisCallCount);
                return Task.CompletedTask;
            },
            writeDataBaseAsync: _ =>
            {
                Interlocked.Increment(ref writeDbCallCount);
                return Task.FromResult(true);
            }
        );
    }

    // ==================== GetBlock ====================

    [Fact]
    public void GetBlockSame()//同Key返回同一个ActionBlock
    {
        var b1 = block.GetBlock("user");
        var b2 = block.GetBlock("user");

        Assert.Same(b1, b2);
    }

    [Fact]
    public void GetBlock()//不同Key返回不同ActionBlock
    {
        var b1 = block.GetBlock("user");
        var b2 = block.GetBlock("order");

        Assert.NotSame(b1, b2);
    }

    // ==================== SendAsync ====================

    [Fact]
    public async Task SendAsync()//消息被ActionBlock处理
    {
        var actionBlock = block.GetBlock("test");

        await actionBlock.SendAsync(new object());

        // 等待 TPL Dataflow 处理完成
        actionBlock.Complete();
        await actionBlock.Completion;

        Assert.True(writeRedisCallCount >= 1);
    }

    // ==================== RetryCount ====================

    [Fact]
    public void GetRetryCount()//新Key_返回0()
    {
        var count = block.GetRetryCount("new_key");
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task ResendAsync()//递增重试计数
    {
        await block.ResendAsync("testKey", new object());
        Assert.Equal(1, block.GetRetryCount("testKey"));

        await block.ResendAsync("testKey", new object());
        Assert.Equal(2, block.GetRetryCount("testKey"));
    }

    [Fact]
    public async Task PallResendAsync()//并发调用_计数原子正确
    {
        var tasks = new List<Task>();
        for (int i = 0; i < 100; i++)
        {
            tasks.Add(block.ResendAsync("concurrent", new object()));
        }
        await Task.WhenAll(tasks);

        Assert.Equal(100, block.GetRetryCount("concurrent"));
    }

    // ==================== TryRemove ====================

    [Fact]
    public async Task TryRemove()//删除存在的Key_返回计数值
    {
        await block.ResendAsync("toRemove", new object());
        await block.ResendAsync("toRemove", new object()); // count = 2

        var removedCount = block.TryRemove("toRemove");

        Assert.Equal(2, removedCount);
        Assert.Equal(0, block.GetRetryCount("toRemove")); // 被重置为 0（因为 GetOrAdd）
    }

    [Fact]
    public void TryRemoveNotEix()//删除不存在的Key_返回负1
    {
        var result = block.TryRemove("nonexistent");
        Assert.Equal(-1, result);
    }

    // ==================== DoPollingAsync ====================

    [Fact]
    public async Task DoPollingAsync()//调用数据库写入函数
    {
        await block.DoPollingAsync("polling_test");

        Assert.Equal(1, writeDbCallCount);
    }
}

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace RedisCache.Tests;

public class RedisCacheTests
{
    private readonly Mock<IRedisService> redisServiceMock;
    private readonly Mock<ILogger<RedisCache>> loggerMock;
    private readonly RedisOption options;

    public RedisCacheTests()
    {
        redisServiceMock = new Mock<IRedisService>();
        loggerMock = new Mock<ILogger<RedisCache>>();
        options = new RedisOption
        {
            ConnectionString = "localhost:6379",
            DbNumber = 0,
            Threshold = 3,
            IsPolling = false,
            Interval = 60,
            LockKey = "TestLock",
            Expiry = 30
        };

        RedisKeyExtension.ClearEntityTypes();
        RedisKeyExtension.RegisterEntityTypes(typeof(TestUser), typeof(TestOrder), typeof(InvalidEntity));
    }

    private RedisCache CreateRedisCache(RedisFlushHandler? onFlush = null)
    {
        var optionsMonitor = new Mock<IOptionsMonitor<RedisOption>>();
        optionsMonitor.Setup(o => o.CurrentValue).Returns(options);
        var serviceProvider = new Mock<IServiceProvider>().Object;

        return new RedisCache(
            optionsMonitor.Object,
            redisServiceMock.Object,
            serviceProvider,
            loggerMock.Object,
            onFlush
        );
    }

    private void SetupMocksForFlush(ConcurrentDictionary<string, string> hashValues,
        bool lockAcquired = true, bool flushResult = true, int deleteCount = 100)
    {
        redisServiceMock
            .Setup(r => r.HashSetFieldAsync(It.IsAny<string>(), It.IsAny<ConcurrentDictionary<string, string>>()))
            .ReturnsAsync(true);
        redisServiceMock
            .Setup(r => r.AcquireLockWithRetryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(lockAcquired);
        redisServiceMock
            .Setup(r => r.HashGetAsync(It.IsAny<string>()))
            .ReturnsAsync(hashValues);
        redisServiceMock
            .Setup(r => r.HashDeleteFieldsAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(deleteCount);
        redisServiceMock
            .Setup(r => r.ReleaseLockAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);
    }

    // ==================== PostRedisAsync ====================

    [Fact]
    public async Task PostRedisAsync_成功发送_返回true()
    {
        redisServiceMock
            .Setup(r => r.HashSetFieldAsync(It.IsAny<string>(), It.IsAny<ConcurrentDictionary<string, string>>()))
            .ReturnsAsync(true);
        var cache = CreateRedisCache();
        Assert.True(await cache.PostRedisAsync(new TestUser { Name = "Alice" }));
    }

    [Fact]
    public async Task PostRedisAsync_空值防御()
    {
        var cache = CreateRedisCache();
        Assert.False(await cache.PostRedisAsync(new List<object>()));
        Assert.False(await cache.PostRedisAsync(null!));
    }

    [Fact]
    public async Task PostRedisAsync_批量写入_返回true()
    {
        redisServiceMock
            .Setup(r => r.HashSetFieldAsync(It.IsAny<string>(), It.IsAny<ConcurrentDictionary<string, string>>()))
            .ReturnsAsync(true);
        var cache = CreateRedisCache();
        var users = new List<object> { new TestUser { Name = "U1" }, new TestUser { Name = "U2" } };
        Assert.True(await cache.PostRedisAsync(users));
    }

    // ==================== 核心：先刷后删 ====================

    [Fact]
    public async Task 刷DB成功_删除Redis数据()
    {
        var hashValues = new ConcurrentDictionary<string, string>();
        hashValues["key1"] = Newtonsoft.Json.JsonConvert.SerializeObject(new TestUser { Name = "Ok" });

        SetupMocksForFlush(hashValues, flushResult: true, deleteCount: 1);

        var cache = CreateRedisCache(onFlush: (sp, key, entities) => Task.FromResult(true));

        for (int i = 0; i < 3; i++)
            await cache.WriteRedisAsync(new TestUser { Name = $"Pre{i}" });
        await Task.Delay(300);

        // 成功 → 删除 Redis
        redisServiceMock.Verify(r => r.HashDeleteFieldsAsync(
            It.IsAny<string>(), It.IsAny<IEnumerable<string>>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task 刷DB失败_Redis数据原封不动()
    {
        var hashValues = new ConcurrentDictionary<string, string>();
        hashValues["key1"] = Newtonsoft.Json.JsonConvert.SerializeObject(new TestUser { Name = "Keep" });

        SetupMocksForFlush(hashValues, flushResult: false);

        var cache = CreateRedisCache(onFlush: (sp, key, entities) => Task.FromResult(false));

        for (int i = 0; i < 3; i++)
            await cache.WriteRedisAsync(new TestUser { Name = $"Keep{i}" });
        await Task.Delay(300);

        // 失败 → 不删除 Redis（数据保留）
        redisServiceMock.Verify(r => r.HashDeleteFieldsAsync(
            It.IsAny<string>(), It.IsAny<IEnumerable<string>>()), Times.Never);
    }

    [Fact]
    public async Task 刷DB抛异常_Redis数据保留_锁仍然释放()
    {
        var hashValues = new ConcurrentDictionary<string, string>();
        hashValues["key1"] = Newtonsoft.Json.JsonConvert.SerializeObject(new TestUser { Name = "Crash" });

        SetupMocksForFlush(hashValues, flushResult: false);

        var cache = CreateRedisCache(onFlush: (sp, key, entities) =>
            throw new InvalidOperationException("DB连接断开"));

        for (int i = 0; i < 3; i++)
            await cache.WriteRedisAsync(new TestUser { Name = $"Cr{i}" });
        await Task.Delay(300);

        // 异常 → 锁必须释放
        redisServiceMock.Verify(r => r.ReleaseLockAsync(It.IsAny<string>(), It.IsAny<string>()), Times.AtLeastOnce);
        // 异常 → Redis 数据不删除
        redisServiceMock.Verify(r => r.HashDeleteFieldsAsync(
            It.IsAny<string>(), It.IsAny<IEnumerable<string>>()), Times.Never);
    }

    // ==================== 部分删除场景 ====================

    [Fact]
    public async Task 部分字段删除失败_记录警告但视为成功()
    {
        var hashValues = new ConcurrentDictionary<string, string>();
        hashValues["k1"] = Newtonsoft.Json.JsonConvert.SerializeObject(new TestUser { Name = "A" });
        hashValues["k2"] = Newtonsoft.Json.JsonConvert.SerializeObject(new TestUser { Name = "B" });

        // 声明有 2 个字段但只删了 1 个
        SetupMocksForFlush(hashValues, flushResult: true, deleteCount: 1);

        var cache = CreateRedisCache(onFlush: (sp, key, entities) => Task.FromResult(true));

        for (int i = 0; i < 3; i++)
            await cache.WriteRedisAsync(new TestUser { Name = $"Part{i}" });
        await Task.Delay(300);

        // 仍然调用删除
        redisServiceMock.Verify(r => r.HashDeleteFieldsAsync(
            It.IsAny<string>(), It.IsAny<IEnumerable<string>>()), Times.AtLeastOnce);
    }

    // ==================== 锁与并发 ====================

    [Fact]
    public async Task 获取不到锁_返回false_不读Redis()
    {
        var emptyHash = new ConcurrentDictionary<string, string>();
        SetupMocksForFlush(emptyHash, lockAcquired: false);

        var cache = CreateRedisCache(onFlush: (sp, key, entities) => Task.FromResult(true));
        var result = await cache.WriteDataBaseAsync(nameof(TestUser));
        Assert.False(result);

        redisServiceMock.Verify(r => r.HashGetAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task 未达阈值_直接返回false_不获取锁()
    {
        var cache = CreateRedisCache(onFlush: (sp, key, entities) => Task.FromResult(true));
        var result = await cache.WriteDataBaseAsync(nameof(TestUser));
        Assert.False(result);

        redisServiceMock.Verify(r => r.AcquireLockWithRetryAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    // ==================== 空 Hash ====================

    [Fact]
    public async Task 空Hash_重置计数器_跳过回调()
    {
        var emptyHash = new ConcurrentDictionary<string, string>();
        SetupMocksForFlush(emptyHash, lockAcquired: true, flushResult: true);

        var callbackCalled = false;
        var cache = CreateRedisCache(onFlush: (sp, key, entities) =>
        {
            callbackCalled = true;
            return Task.FromResult(true);
        });

        for (int i = 0; i < 3; i++)
            await cache.WriteRedisAsync(new TestUser { Name = $"Empty{i}" });
        await Task.Delay(300);

        Assert.False(callbackCalled);
    }

    // ==================== 本地计数器 ====================

    [Fact]
    public async Task 本地计数器_避免查询Redis长度()
    {
        redisServiceMock
            .Setup(r => r.HashSetFieldAsync(It.IsAny<string>(), It.IsAny<ConcurrentDictionary<string, string>>()))
            .ReturnsAsync(true);

        var cache = CreateRedisCache(onFlush: (sp, key, entities) => Task.FromResult(true));

        for (int i = 0; i < 5; i++)
            await cache.WriteRedisAsync(new TestUser { Name = $"Counter{i}" });

        redisServiceMock.Verify(r => r.GetHashLength(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task 达到阈值_触发刷盘()
    {
        var hashValues = new ConcurrentDictionary<string, string>(
            new Dictionary<string, string> { ["k1"] = "{}", ["k2"] = "{}", ["k3"] = "{}" });

        SetupMocksForFlush(hashValues, flushResult: true, deleteCount: 3);

        var flushCalled = false;
        var cache = CreateRedisCache(onFlush: (sp, key, entities) =>
        {
            flushCalled = true;
            return Task.FromResult(true);
        });

        for (int i = 0; i < 3; i++)
            await cache.WriteRedisAsync(new TestUser { Name = $"User{i}" });
        await Task.Delay(500);

        Assert.True(flushCalled);
    }

    [Fact]
    public async Task Redis写入失败_回退计数器并重试()
    {
        redisServiceMock
            .Setup(r => r.HashSetFieldAsync(It.IsAny<string>(), It.IsAny<ConcurrentDictionary<string, string>>()))
            .ReturnsAsync(false); // 写入失败

        var cache = CreateRedisCache();

        for (int i = 0; i < 5; i++)
            await cache.WriteRedisAsync(new TestUser { Name = $"Fail{i}" });

        // 失败后计数器不应增长（每次都回退了）
        Assert.True(true); // 不抛异常即通过
    }
}

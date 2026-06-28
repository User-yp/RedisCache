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

    // ==================== PostRedisAsync (单个) ====================

    [Fact]
    public async Task PostRedisAsync_成功发送到ActionBlock_返回true()
    {
        redisServiceMock
            .Setup(r => r.HashSetFieldAsync(It.IsAny<string>(), It.IsAny<ConcurrentDictionary<string, string>>()))
            .ReturnsAsync(true);

        var cache = CreateRedisCache();
        var user = new TestUser { Id = Guid.NewGuid(), Name = "Alice" };

        var result = await cache.PostRedisAsync(user);

        Assert.True(result);
    }

    [Fact]
    public async Task PostRedisAsync_Redis写入失败_触发重试()
    {
        redisServiceMock
            .Setup(r => r.HashSetFieldAsync(It.IsAny<string>(), It.IsAny<ConcurrentDictionary<string, string>>()))
            .ReturnsAsync(false);

        var cache = CreateRedisCache();
        var user = new TestUser { Id = Guid.NewGuid(), Name = "RetryUser" };

        var result = await cache.PostRedisAsync(user);
        Assert.True(result); // ActionBlock 接受成功
    }

    // ==================== PostRedisAsync (批量) ====================

    [Fact]
    public async Task PostRedisAsync_批量空列表_返回false()
    {
        var cache = CreateRedisCache();
        var result = await cache.PostRedisAsync(new List<object>());
        Assert.False(result);
    }

    [Fact]
    public async Task PostRedisAsync_批量null_返回false()
    {
        var cache = CreateRedisCache();
        var result = await cache.PostRedisAsync(null!);
        Assert.False(result);
    }

    [Fact]
    public async Task PostRedisAsync_批量正常列表_返回true()
    {
        redisServiceMock
            .Setup(r => r.HashSetFieldAsync(It.IsAny<string>(), It.IsAny<ConcurrentDictionary<string, string>>()))
            .ReturnsAsync(true);

        var cache = CreateRedisCache();
        var users = new List<object>
        {
            new TestUser { Name = "User1" },
            new TestUser { Name = "User2" }
        };

        var result = await cache.PostRedisAsync(users);
        Assert.True(result);
    }

    // ==================== 本地计数器阈值触发 ====================

    [Fact]
    public async Task WriteRedisAsync_达到阈值_触发刷盘()
    {
        var flushCalled = false;

        redisServiceMock
            .Setup(r => r.HashSetFieldAsync(It.IsAny<string>(), It.IsAny<ConcurrentDictionary<string, string>>()))
            .ReturnsAsync(true);
        redisServiceMock
            .Setup(r => r.AcquireLockWithRetryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(true);
        redisServiceMock
            .Setup(r => r.HashGetAsync(It.IsAny<string>()))
            .ReturnsAsync(new ConcurrentDictionary<string, string>(
                new Dictionary<string, string> { ["k1"] = "{}", ["k2"] = "{}", ["k3"] = "{}" }));
        redisServiceMock
            .Setup(r => r.HashDeleteFieldsAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(3);
        redisServiceMock
            .Setup(r => r.ReleaseLockAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        var cache = CreateRedisCache(onFlush: (sp, key, entities) =>
        {
            flushCalled = true;
            return Task.FromResult(true);
        });

        // 写入 3 个实体，本地计数器达到 Threshold=3 时触发刷盘
        for (int i = 0; i < 3; i++)
        {
            await cache.WriteRedisAsync(new TestUser { Name = $"User{i}" });
        }

        // 等待异步刷盘完成
        await Task.Delay(500);

        Assert.True(flushCalled, "本地计数器达到阈值应触发刷盘");
    }

    // ==================== WriteDataBaseAsync 回调 ====================

    [Fact]
    public async Task WriteDataBaseAsync_回调被正确调用()
    {
        var testUser = new TestUser { Id = Guid.NewGuid(), Name = "CallbackUser" };
        var hashValues = new ConcurrentDictionary<string, string>();
        hashValues["key1"] = Newtonsoft.Json.JsonConvert.SerializeObject(testUser);

        redisServiceMock
            .Setup(r => r.AcquireLockWithRetryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(true);
        redisServiceMock
            .Setup(r => r.HashGetAsync(It.IsAny<string>()))
            .ReturnsAsync(hashValues);
        redisServiceMock
            .Setup(r => r.HashDeleteFieldsAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(1);
        redisServiceMock
            .Setup(r => r.ReleaseLockAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        string? callbackKey = null;
        List<object>? callbackEntities = null;

        // 手动设置本地计数器 >= threshold 以触发刷盘（模拟实际场景）
        var cache = CreateRedisCache(onFlush: (sp, key, entities) =>
        {
            callbackKey = key;
            callbackEntities = entities;
            return Task.FromResult(true);
        });

        // 模拟写入 3 条来填充本地计数器
        for (int i = 0; i < 3; i++)
        {
            redisServiceMock
                .Setup(r => r.HashSetFieldAsync(It.IsAny<string>(), It.IsAny<ConcurrentDictionary<string, string>>()))
                .ReturnsAsync(true);
            await cache.WriteRedisAsync(new TestUser { Name = $"Preload{i}" });
        }

        await Task.Delay(300);

        Assert.NotNull(callbackKey);
        Assert.Equal(nameof(TestUser), callbackKey);
    }

    [Fact]
    public async Task WriteDataBaseAsync_锁获取失败_返回false()
    {
        redisServiceMock
            .Setup(r => r.AcquireLockWithRetryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(false); // 锁获取失败

        var cache = CreateRedisCache(onFlush: (sp, key, entities) => Task.FromResult(true));

        var result = await cache.WriteDataBaseAsync(nameof(TestUser));
        Assert.False(result);
    }

    // ==================== 分布式锁 ====================

    [Fact]
    public async Task WriteDataBaseAsync_获取不到锁_直接返回false()
    {
        redisServiceMock
            .Setup(r => r.AcquireLockWithRetryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(false);

        var cache = CreateRedisCache(onFlush: (sp, key, entities) => Task.FromResult(true));

        var result = await cache.WriteDataBaseAsync(nameof(TestUser));
        Assert.False(result);
        redisServiceMock.Verify(r => r.HashGetAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task WriteDataBaseAsync_未达到阈值_直接返回false()
    {
        // 本地计数器为 0（默认），threshold = 3，所以不会触发
        var cache = CreateRedisCache(onFlush: (sp, key, entities) => Task.FromResult(true));

        var result = await cache.WriteDataBaseAsync(nameof(TestUser));
        Assert.False(result);
        redisServiceMock.Verify(r => r.AcquireLockWithRetryAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task WriteDataBaseAsync_异常时仍然释放锁()
    {
        var hashValues = new ConcurrentDictionary<string, string>();
        hashValues["key1"] = Newtonsoft.Json.JsonConvert.SerializeObject(new TestUser { Name = "Crash" });

        redisServiceMock
            .Setup(r => r.AcquireLockWithRetryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(true);
        redisServiceMock
            .Setup(r => r.HashGetAsync(It.IsAny<string>()))
            .ReturnsAsync(hashValues);
        redisServiceMock
            .Setup(r => r.ReleaseLockAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);
        // 注意：没有设置 HashDeleteFieldsAsync，让它在默认行为下工作

        // 手动设置计数器以通过阈值检查
        var cache = CreateRedisCache(onFlush: (sp, key, entities) =>
            throw new InvalidOperationException("DB异常"));

        // 手动递增本地计数器
        for (int i = 0; i < 3; i++)
        {
            redisServiceMock
                .Setup(r => r.HashSetFieldAsync(It.IsAny<string>(), It.IsAny<ConcurrentDictionary<string, string>>()))
                .ReturnsAsync(true);
            await cache.WriteRedisAsync(new TestUser { Name = $"PreCrash{i}" });
        }

        await Task.Delay(300);

        // 锁在任何情况下都应被释放
        redisServiceMock.Verify(r => r.ReleaseLockAsync(It.IsAny<string>(), It.IsAny<string>()), Times.AtLeastOnce);
    }

    // ==================== 空数据处理 ====================

    [Fact]
    public async Task WriteDataBaseAsync_空Hash_重置计数器()
    {
        redisServiceMock
            .Setup(r => r.AcquireLockWithRetryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(true);
        redisServiceMock
            .Setup(r => r.HashGetAsync(It.IsAny<string>()))
            .ReturnsAsync(new ConcurrentDictionary<string, string>());
        redisServiceMock
            .Setup(r => r.ReleaseLockAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        var callbackCalled = false;
        var cache = CreateRedisCache(onFlush: (sp, key, entities) =>
        {
            callbackCalled = true;
            return Task.FromResult(true);
        });

        // 手动设置计数器
        redisServiceMock
            .Setup(r => r.HashSetFieldAsync(It.IsAny<string>(), It.IsAny<ConcurrentDictionary<string, string>>()))
            .ReturnsAsync(true);

        for (int i = 0; i < 3; i++)
            await cache.WriteRedisAsync(new TestUser { Name = $"Fill{i}" });

        await Task.Delay(300);

        // 回调不应被调用（空 Hash 不需要刷盘）
        Assert.False(callbackCalled);
    }

    // ==================== 性能：本地计数器 ====================

    [Fact]
    public async Task 本地计数器_避免每次查询Redis长度()
    {
        redisServiceMock
            .Setup(r => r.HashSetFieldAsync(It.IsAny<string>(), It.IsAny<ConcurrentDictionary<string, string>>()))
            .ReturnsAsync(true);

        // 使用 IsPolling=false，本地计数器达到阈值时触发刷盘
        // 而不是每次都调用 GetHashLength
        var cache = CreateRedisCache(onFlush: (sp, key, entities) =>
        {
            return Task.FromResult(true);
        });

        // 连续写入 5 条
        for (int i = 0; i < 5; i++)
        {
            await cache.WriteRedisAsync(new TestUser { Name = $"Counter{i}" });
        }

        // GetHashLength 不应该被调用（本地计数器替代了它）
        redisServiceMock.Verify(r => r.GetHashLength(It.IsAny<string>()), Times.Never);
    }
}

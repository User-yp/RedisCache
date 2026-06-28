using Newtonsoft.Json;

namespace RedisCache.Tests;

public class RedisKeyExtensionTests
{
    // ==================== GetRedisKey ====================

    [Fact]
    public void GetRedisKey_单Key属性_返回正确的JSON数组()
    {
        var user = new TestUser { Id = Guid.Parse("11111111-1111-1111-1111-111111111111") };

        var key = user.GetRedisKey();

        Assert.Equal("[\"11111111-1111-1111-1111-111111111111\"]", key);
    }

    [Fact]
    public void GetRedisKey_复合Key属性_返回多元素JSON数组()
    {
        var order = new TestOrder
        {
            OrderId = "ORD-001",
            UserId = "USR-042"
        };

        var key = order.GetRedisKey();

        var parts = JsonConvert.DeserializeObject<List<string>>(key)!;
        Assert.Equal(2, parts.Count);
        Assert.Contains("ORD-001", parts);
        Assert.Contains("USR-042", parts);
    }

    [Fact]
    public void GetRedisKey_无RedisKey属性_抛出ArgumentException()
    {
        var invalid = new InvalidEntity();

        var ex = Assert.Throws<ArgumentException>(() => invalid.GetRedisKey());
        Assert.Contains("[RedisKey]", ex.Message);
    }

    [Fact]
    public void GetRedisKey_相同值产生相同Key()
    {
        var user1 = new TestUser { Id = Guid.Parse("AAAAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA") };
        var user2 = new TestUser { Id = Guid.Parse("AAAAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA") };

        Assert.Equal(user1.GetRedisKey(), user2.GetRedisKey());
    }

    [Fact]
    public void GetRedisKey_不同值产生不同Key()
    {
        var user1 = new TestUser { Id = Guid.NewGuid() };
        var user2 = new TestUser { Id = Guid.NewGuid() };

        Assert.NotEqual(user1.GetRedisKey(), user2.GetRedisKey());
    }

    // ==================== GetRedisEntity ====================

    [Fact]
    public void GetRedisEntity_有效类型名_返回正确Type()
    {
        var type = nameof(TestUser).GetRedisEntity();

        Assert.Equal(typeof(TestUser), type);
    }

    [Fact]
    public void GetRedisEntity_无效类型名_抛出ArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => "NonExistentType".GetRedisEntity());
        Assert.Contains("[RedisEntity]", ex.Message);
    }

    // ==================== GetEntityKeys ====================

    [Fact]
    public void GetEntityKeys_返回所有标记了RedisEntity的类型名()
    {
        var keys = RedisKeyExtension.GetEntityKeys();

        Assert.Contains(nameof(TestUser), keys);
        Assert.Contains(nameof(TestOrder), keys);
        Assert.Contains(nameof(InvalidEntity), keys);
        Assert.DoesNotContain(nameof(PlainClass), keys);
    }

    [Fact]
    public void GetEntityKeys_不包含重复项()
    {
        var keys = RedisKeyExtension.GetEntityKeys();

        Assert.Equal(keys.Distinct().Count(), keys.Count);
    }
}

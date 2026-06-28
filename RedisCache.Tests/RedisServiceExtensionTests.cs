using StackExchange.Redis;
using System.Collections.Concurrent;

namespace RedisCache.Tests;

public class RedisServiceExtensionTests
{
    // ==================== ToRedisValue ====================

    [Fact]
    public void ToRedisValue_Null_返回RedisValue_Null()
    {
        var result = ((string?)null).ToRedisValue();
        Assert.True(result.IsNull);
    }

    [Fact]
    public void ToRedisValue_字符串_返回对应RedisValue()
    {
        var result = "hello".ToRedisValue();
        Assert.Equal("hello", (string)result!);
    }

    [Fact]
    public void ToRedisValue_值类型_调用ToString()
    {
        var result = 42.ToRedisValue();
        Assert.Equal("42", (string)result!);
    }

    [Fact]
    public void ToRedisValue_对象_序列化为JSON()
    {
        var obj = new { A = 1, B = "test" };
        var result = obj.ToRedisValue();
        Assert.Contains("test", (string)result!);
    }

    // ==================== ToRedisValues ====================

    [Fact]
    public void ToRedisValues_空集合_返回空数组()
    {
        var result = Array.Empty<string>().ToRedisValues();
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void ToRedisValues_正常集合_正确转换()
    {
        var result = new[] { "a", "b", "c" }.ToRedisValues();
        Assert.Equal(3, result.Length);
    }

    // ==================== ToHashEntries ====================

    [Fact]
    public void ToHashEntries_Null_返回空数组()
    {
        var result = ((ConcurrentDictionary<string, string>)null!).ToHashEntries();
        Assert.Empty(result);
    }

    [Fact]
    public void ToHashEntries_空字典_返回空数组()
    {
        var result = new ConcurrentDictionary<string, string>().ToHashEntries();
        Assert.Empty(result);
    }

    [Fact]
    public void ToHashEntries_正常字典_正确转换()
    {
        var dict = new ConcurrentDictionary<string, string>();
        dict["field1"] = "value1";
        dict["field2"] = "value2";

        var result = dict.ToHashEntries();

        Assert.Equal(2, result.Length);
        var names = result.Select(e => e.Name.ToString()).ToHashSet();
        Assert.Contains("field1", names);
        Assert.Contains("field2", names);
    }

    // ==================== ToConcurrentDictionary (HashEntry[]) ====================

    [Fact]
    public void ToConcurrentDictionary_HashEntries_空_返回空字典()
    {
        var result = Array.Empty<HashEntry>().ToConcurrentDictionary();
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void ToConcurrentDictionary_HashEntries_正常转换()
    {
        var entries = new[]
        {
            new HashEntry("k1", "v1"),
            new HashEntry("k2", "v2")
        };

        var result = entries.ToConcurrentDictionary();

        Assert.Equal(2, result.Count);
        Assert.Equal("v1", result["k1"]);
        Assert.Equal("v2", result["k2"]);
    }

    // ==================== ToConcurrentDictionary (RedisValue, string) ====================

    [Fact]
    public void ToConcurrentDictionary_单值_正常转换()
    {
        var result = ((RedisValue)"hello").ToConcurrentDictionary("myField");

        Assert.Single(result);
        Assert.Equal("hello", result["myField"]);
    }

    [Fact]
    public void ToConcurrentDictionary_RedisValue_Null_返回空字典()
    {
        var result = RedisValue.Null.ToConcurrentDictionary("field");
        Assert.Empty(result);
    }

    // ==================== ToConcurrentDictionary (RedisValue[], IEnumerable<string>) ====================

    [Fact]
    public void ToConcurrentDictionary_多值_正常转换()
    {
        var values = new RedisValue[] { "a", "b", "c" };
        var fields = new[] { "f1", "f2", "f3" };

        var result = values.ToConcurrentDictionary(fields);

        Assert.Equal(3, result.Count);
        Assert.Equal("a", result["f1"]);
        Assert.Equal("b", result["f2"]);
        Assert.Equal("c", result["f3"]);
    }

    [Fact]
    public void ToConcurrentDictionary_多值空输入_返回空字典()
    {
        var result = Array.Empty<RedisValue>().ToConcurrentDictionary(new[] { "f1" });
        Assert.Empty(result);
    }
}

using Microsoft.AspNetCore.Mvc;
using RedisCache.Attributes;

namespace RedisCache.WebApi.Controllers;

[Route("api/[action]")]
[ApiController]
public class TestController : ControllerBase
{
    private readonly IRedisCache redisCache;
    private readonly ILogger<TestController> _logger;

    public TestController(IRedisCache redisCache, ILogger<TestController> logger)
    {
        this.redisCache = redisCache;
        _logger = logger;
    }

    /// <summary>
    /// 示例 1：写入单条实体
    /// GET /api/save
    /// </summary>
    [HttpGet]
    public async Task<ActionResult> SaveAsync()
    {
        var entity = new TestEntity("张三", "这是一个测试用户", "VIP");
        var result = await redisCache.PostRedisAsync(entity);
        _logger.LogInformation("SaveAsync: entityId={Id}, result={Result}", entity.Id, result);
        return Ok(new { success = result, entityId = entity.Id });
    }

    /// <summary>
    /// 示例 2：写入单条实体（带参数）
    /// GET /api/save?name=李四&desc=管理员&type=Admin
    /// </summary>
    [HttpGet]
    public async Task<ActionResult> SaveWithParams(string name, string desc, string type)
    {
        var entity = new TestEntity(name, desc, type);
        var result = await redisCache.PostRedisAsync(entity);
        return Ok(new { success = result, entityId = entity.Id, name });
    }

    /// <summary>
    /// 示例 3：批量写入
    /// GET /api/batch-save
    /// </summary>
    [HttpGet]
    public async Task<ActionResult> BatchSaveAsync()
    {
        var entities = new List<object>
        {
            new TestEntity("批量用户1", "描述1", "Normal"),
            new TestEntity("批量用户2", "描述2", "Normal"),
            new TestEntity("批量用户3", "描述3", "VIP"),
            new TestEntity("批量用户4", "描述4", "Admin"),
            new TestEntity("批量用户5", "描述5", "Normal"),
        };

        var result = await redisCache.PostRedisAsync(entities);
        return Ok(new { success = result, count = entities.Count });
    }

    /// <summary>
    /// 示例 4：压力测试 — 连续写入 100 条触发阈值刷盘
    /// GET /api/stress-test?count=100
    /// </summary>
    [HttpGet]
    public async Task<ActionResult> StressTestAsync(int count = 100)
    {
        var successCount = 0;
        var failCount = 0;
        var sw = System.Diagnostics.Stopwatch.StartNew();

        for (int i = 0; i < count; i++)
        {
            var entity = new TestEntity($"压测用户{i}", $"压测描述{i}", "Stress");
            var result = await redisCache.PostRedisAsync(entity);
            if (result) successCount++;
            else failCount++;
        }

        sw.Stop();
        _logger.LogInformation("StressTest: {Success}/{Total} succeeded in {Elapsed}ms",
            successCount, count, sw.ElapsedMilliseconds);

        return Ok(new
        {
            total = count,
            success = successCount,
            fail = failCount,
            elapsedMs = sw.ElapsedMilliseconds
        });
    }

    /// <summary>
    /// 示例 5：并发写入 — 并行写 50 条
    /// GET /api/concurrent-save?concurrency=50
    /// </summary>
    [HttpGet]
    public async Task<ActionResult> ConcurrentSaveAsync(int concurrency = 50)
    {
        var tasks = new List<Task<bool>>();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        for (int i = 0; i < concurrency; i++)
        {
            var idx = i; // 闭包捕获
            tasks.Add(Task.Run(async () =>
            {
                var entity = new TestEntity($"并发用户{idx}", $"并发描述{idx}", $"Type{idx % 3}");
                return await redisCache.PostRedisAsync(entity);
            }));
        }

        var results = await Task.WhenAll(tasks);
        sw.Stop();

        var successCount = results.Count(r => r);
        return Ok(new
        {
            total = concurrency,
            success = successCount,
            fail = concurrency - successCount,
            elapsedMs = sw.ElapsedMilliseconds
        });
    }

    /// <summary>
    /// 示例 6：空列表防御 — 验证空列表被正确拒绝
    /// GET /api/empty-batch
    /// </summary>
    [HttpGet]
    public async Task<ActionResult> EmptyBatchAsync()
    {
        var result = await redisCache.PostRedisAsync(new List<object>());
        return Ok(new { success = result, message = "空列表应返回 false" });
    }
}

// ==================== 测试用实体配置 ====================

[RedisEntity]
public class TestOrder
{
    [RedisKey]
    public string OrderNo { get; set; } = string.Empty;

    [RedisKey]
    public Guid UserId { get; set; }

    public decimal Amount { get; set; }
    public DateTime CreateTime { get; set; } = DateTime.Now;

    public string? Remark { get; set; }

    public TestOrder() { }

    public TestOrder(string orderNo, Guid userId, decimal amount, string? remark = null)
    {
        OrderNo = orderNo;
        UserId = userId;
        Amount = amount;
        Remark = remark;
    }
}

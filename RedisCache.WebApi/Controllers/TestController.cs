using Microsoft.AspNetCore.Mvc;

namespace RedisCache.WebApi.Controllers;

[Route("api/[action]")]
[ApiController]
public class TestController : ControllerBase
{
    private readonly IRedisCache redisCache;

    public TestController(IRedisCache redisCache)
    {
        this.redisCache = redisCache;
    }

    [HttpGet]
    public async Task<ActionResult> SaveAsync()
    {
        TestEntity testEntity = new TestEntity("1", "2", "3");
        var res = await redisCache.PostRedisAsync(testEntity);
        return Ok(res);
    }
}

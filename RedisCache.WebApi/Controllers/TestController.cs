using Microsoft.AspNetCore.Mvc;

namespace RedisCache.WebApi.Controllers;

[Route("api/[action]")]
[ApiController]
public class TestController:ControllerBase
{
    private readonly RedisCacheService redisCache;

    public TestController(RedisCacheService redisCache)
    {
        this.redisCache = redisCache;
    }

    [HttpPost]
    public async Task<ActionResult> TestAsync()
    {
        TestEntity testEntity = new()
        {
            Id = Guid.NewGuid(),
            Name = "1",
            Description= "2",
            Type="01"
        };
        await redisCache.AddRedisAsync(testEntity);
        return Ok();
    }
}

using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using RedisCache.DbService;
using System.Diagnostics.SymbolStore;

namespace RedisCache.WebApi.Controllers;

[Route("api/[action]")]
[ApiController]
public class TestController:ControllerBase
{
    private readonly DomainService domain;
    private readonly ICacheContext cacheContext;
    private readonly LRUCache lruCache;

    public TestController(DomainService domain,ICacheContext cacheContext,LRUCache lruCache)
    {
        this.domain = domain;
        this.cacheContext = cacheContext;
        this.lruCache = lruCache;
    }

    [HttpPost]
    public async Task<ActionResult> TestAsync()
    {
        await domain.AddTestEntityAsync();
        return Ok();
    }
    [HttpGet]
    public async Task<ActionResult<List<TestEntity>>> CacheContextTestAsync()
    {
        return await cacheContext.GetAllByKeyAsync<TestEntity>(nameof(TestEntity));
        //return Ok();
    }
    [HttpGet]
    public async Task<ActionResult> LRUCacheTestAsync()
    {
        var str = "FD84DD5E-F3FA-4AB2-92B0-EEEA8F2991D1".ToLower();
        List<string> strings = [str];
         await lruCache.GetAsync<TestEntity>(nameof(TestEntity),JsonConvert.SerializeObject(strings));
        return Ok();
    }
}

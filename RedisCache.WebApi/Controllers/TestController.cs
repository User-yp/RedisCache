using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using RedisCache.DbService;
using RedisCache.ReadService;

namespace RedisCache.WebApi.Controllers;

[Route("api/[action]")]
[ApiController]
public class TestController:ControllerBase
{
    private readonly DomainService domain;
    private readonly IDBContext dBContext;
    private readonly IReadCache readCache;

    public TestController(DomainService domain,IDBContext dBContext, IReadCache readCache)
    {
        this.domain = domain;
        this.dBContext = dBContext;
        this.readCache = readCache;
    }

    [HttpPost]
    public async Task<ActionResult> TestAsync()
    {
        await domain.AddTestEntityAsync();
        return Ok();
    }
    [HttpGet]
    public async Task<ActionResult> CacheContextTestAsync()
    {
         await dBContext.GetAllAsync<TestEntity>(typeof(TestEntity).Name);
        return Ok();
    }
    [HttpGet]
    public async Task<ActionResult> LRUCacheTestAsync()
    {
        var str = "9B428F9E-6B3A-43C5-B511-077595906E25".ToLower();
        List<string> strings = [str];
        var res= await readCache.GetAsync<TestEntity>(nameof(TestEntity),JsonConvert.SerializeObject(strings));
        return Ok();
    }
}

﻿using Microsoft.AspNetCore.Mvc;
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
         await dBContext.GetAllAsync(typeof(TestEntity).Name);
        return Ok();
    }
    [HttpGet]
    public async Task<ActionResult> LRUCacheTestAsync()
    {
        var str = "FD84DD5E-F3FA-4AB2-92B0-EEEA8F2991D1".ToLower();
        List<string> strings = [str];
         await readCache.GetAsync<TestEntity>(nameof(TestEntity),JsonConvert.SerializeObject(strings));
        return Ok();
    }
}

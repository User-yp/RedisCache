using Microsoft.AspNetCore.Mvc;

namespace RedisCache.WebApi.Controllers;

[Route("api/[action]")]
[ApiController]
public class TestController:ControllerBase
{
    private readonly DomainService domain;

    public TestController(DomainService domain)
    {
        this.domain = domain;
    }

    [HttpPost]
    public async Task<ActionResult> TestAsync()
    {
        await domain.AddTestEntityAsync();
        return Ok();
    }
}

using RedisCache.WriteService;

namespace RedisCache.WebApi;

public class DomainService
{
    private readonly IWriteCache writeCache;

    public DomainService(IWriteCache writeCache)
    {
        this.writeCache = writeCache;
    }
    public async Task AddTestEntityAsync()
    {
        Random random = new Random();
        List<TestEntity> entities = new List<TestEntity>(); 
        for (int i = 0; i < 1; i++)
        {
            var str = random.Next(1000, 9999).ToString();
            TestEntity testEntity = new()
            {
                Id = Guid.NewGuid(),
                Name = str,
                Description = str,
                Type = DateTime.Now.ToString()
            };
            entities.Add(testEntity);
            await writeCache.AddRedisAsync(testEntity);
        }
        //await writeCache.AddRedisAsync(entities);
    }
}

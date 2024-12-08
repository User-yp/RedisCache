using RedisCache.Domain.Entity;
using RedisCache.WriteService;

namespace RedisCache.Domain;

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
        for (int i = 0; i < 20; i++)
        {
            var str = random.Next(1000, 9999).ToString();
            TestEntity testEntity = new(str, str, DateTime.Now.ToString());
            entities.Add(testEntity);
            //await writeCache.AddRedisAsync(testEntity);
        }
        /*await dbContext.AddRangeAsync(entities);
        await dbContext.SaveChangesAsync();*/
        await writeCache.AddRedisAsync(entities);
    }
}

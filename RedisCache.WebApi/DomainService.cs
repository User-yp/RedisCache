namespace RedisCache.WebApi;

public class DomainService
{
    private readonly IRedisCache redisCache;

    public DomainService(BaseDbContext dbContext,IRedisCache redisCache)
    {
        this.redisCache = redisCache;
    }
    public async Task AddTestEntityAsync()
    {
        Random random = new Random();
        List<TestEntity> entities = new List<TestEntity>(); 
        for (int i = 0; i < 5; i++)
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
            await redisCache.AddRedisAsync(testEntity);
        }
        //await redisCache.AddRedisAsync(entities);
    }
}

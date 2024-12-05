using RedisCache.DBService;

namespace RedisCache.Middel;

public class ReadContext<T> : BaseRepository<T>, IReadContext<T> 
{
    public ReadContext(BaseDbContext dbContext) : base(dbContext.Database)
    {
    }
}

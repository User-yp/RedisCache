using Microsoft.EntityFrameworkCore;
using RedisCache.Domain.Entity;
using RedisCache.Domain.IRepository;
using RedisCache.ReadService;

namespace RedisCache.Infrastructure.Repository;

public class BookRepository: IBookRepository
{
    private readonly BaseDbContext dbContext;
    private readonly IReadCache readCache;

    public BookRepository(BaseDbContext dbContext,IReadCache readCache)
    {
        this.dbContext = dbContext;
        this.readCache = readCache;
    }
    public async Task<Book?> GetBookById(string redisKey)
    {
        var res= await readCache.GetAsync<Book>(nameof(Book), redisKey);
        if (res != null)
        {
            if (await dbContext.Authors.AnyAsync(a => a.Bookrs.Any(b => b.Id == res.Id)))
                await readCache.SetKeyAsync<Author>(nameof(Author));
        }
        return res;
    }
}

using Microsoft.EntityFrameworkCore;

namespace RedisCache.DBService;

public abstract class BaseRepository<T> : IBaseRepository<T> where T : RootEntity
{
    private readonly DbSet<T> dbSet;

    protected BaseRepository(DbSet<T> dbSet)
    {
        this.dbSet = dbSet;
    }

    public async Task CreateAsync(T entity)
    {
        await dbSet.AddAsync(entity);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var res = await GetAsync(id);
        if (res == null)
            return false;
        return true;
    }

    public async Task<T?> GetAsync(Guid id)
    {
        return await dbSet.FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<List<T>?> GetAllAsync()
    {
        return await dbSet.ToListAsync();
    }


}

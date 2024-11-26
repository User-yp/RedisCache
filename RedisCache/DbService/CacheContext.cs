using Microsoft.EntityFrameworkCore;
using RedisCache.Attributes;
using System.Reflection;

namespace RedisCache.DbService;

public class CacheContext
{
    public CacheContext() { }
    public async Task<bool> InsertDatabase<T>(List<T> values) where T : class
    {
        (var dbContextType, var dbSetType) = values[0].GetRedisDbSet();

        using (var dbContext = (DbContext)Activator.CreateInstance(dbContextType))
        {
            // 获取 DbSet  
            var dbSet = (DbSet<T>)dbSetType.GetValue(dbContext);
            // 添加值  
            await dbSet.AddRangeAsync(values);
            // 保存更改  
            await dbContext.SaveChangesAsync();
        }
        return await Task.FromResult(true);
    }
}

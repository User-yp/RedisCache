using Microsoft.EntityFrameworkCore;
using RedisCache.Attributes;
using System.Reflection;

namespace RedisCache.DbService;

public class CacheContext: ICacheContext
{
    public CacheContext() { }
    public async Task<bool> InsertAsync<T>(T values) where T : class
    {
        (var dbContextType, var dbSetType) = values.GetRedisDbSet();

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
    public async Task<bool> InsertDatabase<T>(List<T> values) where T : class
    {
        (var dbContextType, var dbSetType) = values[0].GetRedisDbSet();
        using (var dbContext = (DbContext)Activator.CreateInstance(dbContextType))
        {
            var dbSet = (DbSet<T>)dbSetType.GetValue(dbContext);
            await dbSet.AddRangeAsync(values);
            await dbContext.SaveChangesAsync();
        }
        return await Task.FromResult(true);
    }
    public async Task<bool> DeletedAsync<T>(T instance) where T : class
    {
        (var dbContextType, var dbSetType) = instance.GetRedisDbSet();
        using (var dbContext = (DbContext)Activator.CreateInstance(dbContextType))
        {
            var dbSet = (DbSet<T>)dbSetType.GetValue(dbContext);
            dbSet.Remove(instance);
            await dbContext.SaveChangesAsync();
        }
        return await Task.FromResult(true);
    }
    public async Task<bool> DeletedAsync<T>(List<T> instances) where T : class
    {
        (var dbContextType, var dbSetType) = instances.GetRedisDbSet();
        using (var dbContext = (DbContext)Activator.CreateInstance(dbContextType))
        {
            var dbSet = (DbSet<T>)dbSetType.GetValue(dbContext);
            dbSet.RemoveRange(instances);
            await dbContext.SaveChangesAsync();
        }
        return await Task.FromResult(true);
    }
    public async Task<List<T>?> GetAllAsync<T>(T instances) where T : class
    {
        (var dbContextType, var dbSetType) = instances.GetRedisDbSet();
        using var dbContext = (DbContext)Activator.CreateInstance(dbContextType);
        var dbSet = (DbSet<T>)dbSetType.GetValue(dbContext);
        var values = dbSet?.ToList();
        return values;

    }
}

using Microsoft.EntityFrameworkCore;
using RedisCache.Attributes;
using RedisCache.DbService;

namespace RedisCache.DBService;

public class DBContext : IDBContext
{
    public DBContext() { }
    /*public async Task<bool> InsertAsync<T>(params T[] values) where T : class
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
    }*/
    public async Task<bool> InsertDatabase<T>(params T[] values) where T : class
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
    /*public async Task<bool> DeletedAsync<T>(T instance) where T : class
    {
        (var dbContextType, var dbSetType) = instance.GetRedisDbSet();
        using (var dbContext = (DbContext)Activator.CreateInstance(dbContextType))
        {
            var dbSet = (DbSet<T>)dbSetType.GetValue(dbContext);
            dbSet.Remove(instance);
            await dbContext.SaveChangesAsync();
        }
        return await Task.FromResult(true);
    }*/
    public async Task<bool> DeletedAsync<T>(params T[] instances) where T : class
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
    /*public async Task<List<T>?> GetAllByKeyAsync<T>(string key) where T : class
    {
        (var dbContextType, var dbSetType) = key.GetRedisDbSet();
        using var dbContext = (DbContext)Activator.CreateInstance(dbContextType);
        var dbSet = (DbSet<T>)dbSetType.GetValue(dbContext);
        var values = dbSet?.ToList();
        return values;

    }*/
}

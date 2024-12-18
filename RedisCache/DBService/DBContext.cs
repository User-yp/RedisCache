﻿using Microsoft.EntityFrameworkCore;
using RedisCache.Attributes;
using RedisCache.DbService;

namespace RedisCache.DBService;

public class DBContext : IDBContext
{
    public DBContext() { }
    public async Task<bool> InsertDatabase<T>(List<T> values) where T : RootEntity
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
    public async Task<bool> DeletedAsync<T>(List<T> instances) where T : RootEntity
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
    public async Task<List<T>?> GetAllAsync<T>(string instances) where T : RootEntity
    {
        (var dbContextType, var dbSetType) = instances.GetRedisDbSet();
        using var dbContext = (DbContext)Activator.CreateInstance(dbContextType);
        var dbSet = (DbSet<T>)dbSetType.GetValue(dbContext);
        var values = dbSet?.ToList();
        return values;
    }
    public async Task<T> GetAsync<T>(string key, string redisKey) where T : RootEntity
    {
        (var dbContextType, var dbSetType) = key.GetRedisDbSet();
        using var dbContext = (DbContext)Activator.CreateInstance(dbContextType);
        var dbSet = (DbSet<T>)dbSetType.GetValue(dbContext);
        var value = await dbSet?.FirstOrDefaultAsync(t => t.RedisKey == redisKey);
        return value;
    }
}

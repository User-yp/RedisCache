using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RedisCache.DbService;

public interface ICacheContext
{
    Task<bool> InsertAsync<T>(T values) where T : class;
    Task<bool> InsertDatabase<T>(List<T> values) where T : class;
    Task<bool> DeletedAsync<T>(T instance) where T : class;
    Task<bool> DeletedAsync<T>(List<T> instances) where T : class;
    Task<List<T>?> GetAllAsync<T>(T instances) where T : class;
}

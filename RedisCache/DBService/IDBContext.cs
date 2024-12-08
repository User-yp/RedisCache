using RedisCache.DBService;

namespace RedisCache.DbService;

public interface IDBContext
{
    Task<bool> InsertDatabase<T>(List<T> values) where T : RootEntity;
    Task<bool> DeletedAsync<T>(List<T> instances) where T : RootEntity;
    Task<List<T>?> GetAllAsync<T>(string instances) where T : RootEntity;
    Task<T?> GetAsync<T>(string key, string fieldKey) where T : RootEntity;
}

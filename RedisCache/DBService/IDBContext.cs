namespace RedisCache.DbService;

public interface IDBContext
{
    Task<bool> InsertDatabase<T>(params T[] values) where T : class;
    Task<bool> DeletedAsync<T>(params T[] instances) where T : class;
    Task<List<T>?> GetAllAsync<T>(string instances) where T : class;
    Task<T?> GetAsync<T>(string key, string fieldKey) where T : class;
}

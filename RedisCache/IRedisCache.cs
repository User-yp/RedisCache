using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RedisCache;

public interface IRedisCache
{
    Task AddRedisAsync<T>(T value) where T : class;
    Task AddRedisAsync<T>(List<T> values) where T : class;
}

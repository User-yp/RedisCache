using RedisCache.Domain.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RedisCache.Domain.IRepository;

public interface IBookRepository
{
    Task<Book?> GetBookById(string redisKey);
}

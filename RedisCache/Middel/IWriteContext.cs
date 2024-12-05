using RedisCache.DBService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RedisCache.Middel;

public interface IWriteContext<T> : IBaseRepository<T>
{
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RedisCache.Options;

public class LRUOptions
{
    public int Capacity { get; set; }
    public int ExpiryTime { get; set; }
    public LRUOptions() { }
    public LRUOptions(int capacity, int expiryTime)
    {
        Capacity = capacity;
        ExpiryTime = expiryTime;
    }
}

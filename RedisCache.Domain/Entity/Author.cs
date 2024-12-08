using RedisCache.Attributes;
using RedisCache.DBService;

namespace RedisCache.Domain.Entity;
[RedisEntity]
public class Author:RootEntity
{
    [RedisKey]
    public string Name { get; set; }
    public int Age { get; set; }
    public string Description { get; set; }

    // 多对多关系，一个作者可以有多本书  
    public ICollection<Book> Bookrs { get; set; } = [];
    public Author() { }
    public Author(string Name, int Age, string Description)
    {
        this.Name = Name;
        this.Age = Age;
        this.Description = Description;
        RedisKey = this.GetRedisKey();
    }
}
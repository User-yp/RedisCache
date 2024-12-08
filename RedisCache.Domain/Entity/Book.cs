using RedisCache.Attributes;
using RedisCache.DBService;

namespace RedisCache.Domain.Entity;
[RedisEntity]
public class Book:RootEntity
{
    [RedisKey]
    public string BookName { get; set; }
    public string Title { get; set; }
    public DateTime PublishTime { get; set; }
    public decimal Price { get; set; }
    public string Category { get; set; }
    // 多对多关系，一个书可以有多个作者，一个作者可以写多本书  
    public ICollection<Author> Authors { get; set; } = [];
    public Book() { }
    public Book(string BookName,string Title,decimal Price, string Category)
    {
        this.BookName = BookName;
        this.Title = Title;
        this.PublishTime= DateTime.Now;
        this.Price = Price;
        this.Category = Category;
        RedisKey = this.GetRedisKey();
    }
    public void SetAuthors(params Author[] authors)
    {
        foreach (var author in authors)
            Authors.Add(author);
    }
}
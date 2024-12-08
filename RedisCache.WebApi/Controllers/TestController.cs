using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using RedisCache.DbService;
using RedisCache.Domain;
using RedisCache.Domain.Entity;
using RedisCache.Domain.IRepository;
using RedisCache.Infrastructure;
using RedisCache.ReadService;

namespace RedisCache.WebApi.Controllers;

[Route("api/[action]")]
[ApiController]
public class TestController:ControllerBase
{
    private readonly BaseDbContext context;
    private readonly DomainService domain;
    private readonly IDBContext dBContext;
    private readonly IReadCache readCache;
    private readonly IBookRepository bookRepository;

    public TestController(BaseDbContext context, DomainService domain,IDBContext dBContext, IReadCache readCache
        ,IBookRepository bookRepository)
    {
        this.context = context;
        this.domain = domain;
        this.dBContext = dBContext;
        this.readCache = readCache;
        this.bookRepository = bookRepository;
    }

    [HttpPost]
    public async Task<ActionResult> InitAsync()
    {
        var author1 = new Author("李建华", 28, "新一代写实派年轻作家，其代表作有长篇小说《风云初记》、中篇小说《铁木前传》");
        var author2  =  new Author("刘长宇", 32, "先后担任中国作家协会主席、上海市作家协会主席等职，代表作短篇小说《荷花淀》；小说散文结集《白洋淀纪事》。");
        var author3  =  new Author("丁玲", 34, "代表作《莎菲女士的日记》《太阳照在桑干河上》。1951年《太阳照在桑干河上》获斯大林文学奖二等奖。");
        var author4  =  new Author("周立波", 32, "现代作家，原名周绍仪，湖南益阳人。其代表作有长篇小说《暴风骤雨》，曾荣获斯大林文学奖。解放后，他参加《解放了的中国》彩色影片摄制工作，再次荣获斯大林文学奖。");
        var author5  =  new Author("徐迟", 32, "现代诗人、报告文学作家。原名徐高寿；浙江省吴兴人。他前期作品有诗集《二十岁的人》，散文集《美文集》");
        List<Author> authors = [author1, author2, author3, author4, author5];

        var book1 = new Book("风云初记", "风云初记", 36.6m, "长篇小说");
        var book12 = new Book("铁木前传", "铁木前传", 18.8m, "中篇小说");
        var book21 = new Book("荷花淀", "荷花淀", 26.6m, "短篇小说");
        var book22 = new Book("白洋淀纪事", "白洋淀纪事", 26.6m, "小说散文结集");
        var book2 = new Book("太阳照在桑干河上", "太阳照在桑干河上", 26.6m, "小说");
        var book3 = new Book("莎菲女士的日记", "莎菲女士的日记", 55.6m, "长篇小说");
        var book4 = new Book("暴风骤雨", "暴风骤雨", 24.6m, "日记");
        var book5 = new Book("解放了的中国", "解放了的中国", 15.6m, "纪实");
        var book6 = new Book("二十岁的人", "二十岁的人", 69.6m, "诗集");
        var book7 = new Book("美文集", "美文集", 39.9m, "散文集");
        book1.SetAuthors(author1, author2);
        book12.SetAuthors(author1);
        book21.SetAuthors(author3, author2);
        book22.SetAuthors( author2);
        book2.SetAuthors(author3);
        book3.SetAuthors(author3);
        book4.SetAuthors(author4, author2);
        book5.SetAuthors(author4, author1);
        book6.SetAuthors(author5, author3);
        book7.SetAuthors(author5, author2);
        List<Book> books = [book1, book12, book21, book22, book2, book3, book4, book5, book6, book7];
        await context.AddRangeAsync(books);
        await context.AddRangeAsync(authors);
        await context.SaveChangesAsync();
        return Ok();
    }
    [HttpGet]
    public async Task<ActionResult<List<Book>>> CacheContextTestAsync()
    {
        var res= await dBContext.GetAllAsync<Book>(nameof(Book));
        return Ok(res);
    }
    [HttpGet]
    public async Task<ActionResult> LRUCacheTestAsync()
    {
        var str = "9B428F9E-6B3A-43C5-B511-077595906E25".ToLower();
        List<string> strings = [str];
        var res= await readCache.GetAsync<TestEntity>(nameof(TestEntity),JsonConvert.SerializeObject(strings));
        return Ok();
    }
    [HttpGet]
    public async Task<ActionResult<TestEntity>> GetOneTestAsync()
    {
        return Ok();
    }
    [HttpGet]
    public async Task<ActionResult<Book>> GetBookByIdAsync()
    {
        string fieldKey = "[\"莎菲女士的日记\",\"ecca8803-fb4d-4373-be5b-1c2244c81622\"]";
        var res = await bookRepository.GetBookById(fieldKey);
        return Ok(res);
    }
}

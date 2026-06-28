using Microsoft.EntityFrameworkCore;
using RedisCache;
using RedisCache.DBService;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 注册 EF Core DbContext
builder.Services.AddDbContext<BaseDbContext>(options =>
    options.UseSqlServer(Environment.GetEnvironmentVariable("ConnStr")));

// 注册 RedisCache — 通过回调委托实现数据刷盘
builder.Services.AddRedisCache(builder.Configuration, onFlush: async (sp, key, entities) =>
{
    using var scope = sp.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<BaseDbContext>();

    // 一行代码：EF Core 根据每个 entity 的 CLR 类型自动路由到对应的 DbSet
    db.AddRange(entities);
    await db.SaveChangesAsync();
    return true;
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();

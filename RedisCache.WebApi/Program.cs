using Microsoft.EntityFrameworkCore;
using RedisCache;
using RedisCache.Attributes;
using RedisCache.DBService;
using RedisCache.WebApi;

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

    // 根据实体类型分发到对应的 DbSet
    // 实际项目中建议写类型分发逻辑或使用 switch expression
    var entityType = key.GetRedisEntity();
    if (entityType == typeof(TestEntity))
    {
        db.Set<TestEntity>().AddRange(entities.Cast<TestEntity>());
    }
    // else if (entityType == typeof(OtherEntity)) { ... }

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

using Microsoft.EntityFrameworkCore;
using RedisCache.WebApi;

namespace RedisCache.DBService;

/// <summary>
/// 示例 DbContext — 标准 EF Core 用法，无需任何 Redis 相关 Attribute
/// </summary>
public class BaseDbContext : DbContext
{
    public DbSet<TestEntity> TestEntity { get; set; }

    public BaseDbContext(DbContextOptions<BaseDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly);
    }
}

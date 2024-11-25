using Microsoft.EntityFrameworkCore;
using RedisCache.Attributes;
using System.Reflection;

namespace RedisCache.WebApi;
[RedisDbContext]
public class BaseDbContext:DbContext
{
    public DbSet<TestEntity> testEntities {  get; set; }
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        optionsBuilder.UseSqlServer(Environment.GetEnvironmentVariable("ConnStr"));
    }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly);
    }
}

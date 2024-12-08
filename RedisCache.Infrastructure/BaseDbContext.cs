using Microsoft.EntityFrameworkCore;
using RedisCache.Attributes;
using RedisCache.Domain.Entity;

namespace RedisCache.Infrastructure;
[RedisDbContext]
public class BaseDbContext : DbContext
{
    public DbSet<TestEntity> TestEntities { get; set; }
    public DbSet<Book> Books { get; set; }
    public DbSet<Author> Authors { get; set; }
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

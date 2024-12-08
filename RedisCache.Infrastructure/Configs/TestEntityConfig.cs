using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RedisCache.Domain.Entity;

namespace RedisCache.Infrastructure.Configs;

public class TestEntityConfig : IEntityTypeConfiguration<TestEntity>
{
    public void Configure(EntityTypeBuilder<TestEntity> builder)
    {
        builder.ToTable($"T_{nameof(TestEntity)}");
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RedisCache.Domain.Entity;

namespace RedisCache.Infrastructure.Configs;

public class AuthorConfig : IEntityTypeConfiguration<Author>
{
    public void Configure(EntityTypeBuilder<Author> builder)
    {
        builder.ToTable($"T_{nameof(Author)}");
    }
}

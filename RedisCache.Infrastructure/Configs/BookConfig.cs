using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RedisCache.Domain.Entity;

namespace RedisCache.Infrastructure.Configs;

public class BookConfig : IEntityTypeConfiguration<Book>
{
    public void Configure(EntityTypeBuilder<Book> builder)
    {
        builder.ToTable($"T_{nameof(Book)}");
        builder.HasMany(b => b.Authors).WithMany(a => a.Bookrs).UsingEntity($"T_BookAuthors",j =>
        {
            j.IndexerProperty<int>("Id");
            j.HasKey("Id");
        });
    }
}

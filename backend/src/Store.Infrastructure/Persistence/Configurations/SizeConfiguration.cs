using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Store.Domain.Entities;

namespace Store.Infrastructure.Persistence.Configurations;

public class SizeConfiguration : IEntityTypeConfiguration<Size>
{
    public void Configure(EntityTypeBuilder<Size> builder)
    {
        builder.ToTable("Sizes");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Name).HasMaxLength(10);

        builder.HasIndex(s => s.Name).IsUnique();

        builder.HasData(
            new { Id = 1, Name = "XS", DisplayOrder = 1 },
            new { Id = 2, Name = "S", DisplayOrder = 2 },
            new { Id = 3, Name = "M", DisplayOrder = 3 },
            new { Id = 4, Name = "L", DisplayOrder = 4 },
            new { Id = 5, Name = "XL", DisplayOrder = 5 },
            new { Id = 6, Name = "XXL", DisplayOrder = 6 },
            new { Id = 7, Name = "38", DisplayOrder = 10 },
            new { Id = 8, Name = "39", DisplayOrder = 11 },
            new { Id = 9, Name = "40", DisplayOrder = 12 },
            new { Id = 10, Name = "41", DisplayOrder = 13 },
            new { Id = 11, Name = "42", DisplayOrder = 14 },
            new { Id = 12, Name = "43", DisplayOrder = 15 },
            new { Id = 13, Name = "44", DisplayOrder = 16 },
            new { Id = 14, Name = "45", DisplayOrder = 17 });
    }
}

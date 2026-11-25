using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Store.Domain.Entities;

namespace Store.Infrastructure.Persistence.Configurations;

public class ColorConfiguration : IEntityTypeConfiguration<Color>
{
    public void Configure(EntityTypeBuilder<Color> builder)
    {
        builder.ToTable("Colors");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name).HasMaxLength(50);
        builder.Property(c => c.HexCode).HasMaxLength(7);

        builder.HasIndex(c => c.Name).IsUnique();

        builder.HasData(
            new { Id = 1, Name = "Black", HexCode = "#000000" },
            new { Id = 2, Name = "White", HexCode = "#FFFFFF" },
            new { Id = 3, Name = "Navy", HexCode = "#1F2A44" },
            new { Id = 4, Name = "Gray", HexCode = "#8E8E8E" },
            new { Id = 5, Name = "Beige", HexCode = "#D8C8A8" },
            new { Id = 6, Name = "Red", HexCode = "#C0392B" },
            new { Id = 7, Name = "Blue", HexCode = "#2E6DB4" },
            new { Id = 8, Name = "Brown", HexCode = "#6B4423" });
    }
}

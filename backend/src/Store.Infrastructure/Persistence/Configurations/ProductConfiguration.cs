using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Store.Domain.Entities;

namespace Store.Infrastructure.Persistence.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products", t => t.HasCheckConstraint("CK_Products_Price", "[Price] > 0"));
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name).HasMaxLength(200);
        builder.Property(p => p.Slug).HasMaxLength(200);
        builder.Property(p => p.Description).HasMaxLength(2000);
        builder.Property(p => p.Price).HasPrecision(18, 2);

        builder.HasIndex(p => p.Slug).IsUnique();
        builder.HasIndex(p => p.CategoryId);
        builder.HasIndex(p => new { p.IsActive, p.IsFeatured });

        builder.HasMany(p => p.Variants)
            .WithOne(v => v.Product)
            .HasForeignKey(v => v.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.Images)
            .WithOne(i => i.Product)
            .HasForeignKey(i => i.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

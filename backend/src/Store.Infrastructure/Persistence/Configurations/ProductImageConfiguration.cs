using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Store.Domain.Entities;

namespace Store.Infrastructure.Persistence.Configurations;

public class ProductImageConfiguration : IEntityTypeConfiguration<ProductImage>
{
    public void Configure(EntityTypeBuilder<ProductImage> builder)
    {
        builder.ToTable("ProductImages");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Url).HasMaxLength(500);

        builder.HasOne(i => i.Color)
            .WithMany()
            .HasForeignKey(i => i.ColorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

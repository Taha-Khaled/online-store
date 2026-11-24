using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Store.Domain.Entities;

namespace Store.Infrastructure.Persistence.Configurations;

public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("OrderItems", t => t.HasCheckConstraint("CK_OrderItems_Quantity", "[Quantity] BETWEEN 1 AND 5"));
        builder.HasKey(i => i.Id);

        builder.Property(i => i.ProductName).HasMaxLength(200);
        builder.Property(i => i.ColorName).HasMaxLength(50);
        builder.Property(i => i.SizeName).HasMaxLength(10);
        builder.Property(i => i.UnitPrice).HasPrecision(18, 2);

        builder.HasIndex(i => new { i.OrderId, i.ProductVariantId }).IsUnique();
    }
}

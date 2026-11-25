using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Store.Domain.Entities;

namespace Store.Infrastructure.Persistence.Configurations;

public class CartItemConfiguration : IEntityTypeConfiguration<CartItem>
{
    public void Configure(EntityTypeBuilder<CartItem> builder)
    {
        builder.ToTable("CartItems", t => t.HasCheckConstraint("CK_CartItems_Quantity", "[Quantity] BETWEEN 1 AND 5"));
        builder.HasKey(c => c.Id);

        builder.HasIndex(c => new { c.UserId, c.ProductVariantId }).IsUnique();
    }
}

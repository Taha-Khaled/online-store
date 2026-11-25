namespace Store.Domain.Entities;

public class CartItem
{
    public CartItem(int userId, int productVariantId, int quantity, DateTime addedAt)
    {
        UserId = userId;
        ProductVariantId = productVariantId;
        Quantity = quantity;
        AddedAt = addedAt;
    }

    public int Id { get; private set; }
    public int UserId { get; private set; }
    public int ProductVariantId { get; private set; }
    public int Quantity { get; private set; }
    public DateTime AddedAt { get; private set; }

    public User User { get; private set; } = null!;
    public ProductVariant ProductVariant { get; private set; } = null!;
}

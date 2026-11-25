namespace Store.Domain.Entities;

public class CartItem
{
    public CartItem(int userId, int productVariantId, int quantity, DateTime addedAt)
    {
        EnsureValidQuantity(quantity);

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

    /// <summary>Sets the line quantity (1 to <see cref="StoreLimits.MaxQuantityPerVariant"/>).</summary>
    public void SetQuantity(int quantity)
    {
        EnsureValidQuantity(quantity);
        Quantity = quantity;
    }

    private static void EnsureValidQuantity(int quantity)
    {
        if (quantity is < 1 or > StoreLimits.MaxQuantityPerVariant)
        {
            throw new DomainException(
                $"Cart quantity must be between 1 and {StoreLimits.MaxQuantityPerVariant}.");
        }
    }
}

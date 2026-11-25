namespace Store.Domain.Entities;

public class OrderItem
{
    public OrderItem(
        int productVariantId,
        string productName,
        string colorName,
        string sizeName,
        decimal unitPrice,
        int quantity)
    {
        ProductVariantId = productVariantId;
        ProductName = productName;
        ColorName = colorName;
        SizeName = sizeName;
        UnitPrice = unitPrice;
        Quantity = quantity;
    }

    public int Id { get; private set; }
    public int OrderId { get; private set; }
    public int ProductVariantId { get; private set; }
    public string ProductName { get; private set; }
    public string ColorName { get; private set; }
    public string SizeName { get; private set; }
    public decimal UnitPrice { get; private set; }
    public int Quantity { get; private set; }

    public Order Order { get; private set; } = null!;
    public ProductVariant ProductVariant { get; private set; } = null!;
}

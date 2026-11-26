namespace Store.Domain.Entities;

public class ProductVariant
{
    public ProductVariant(int productId, int colorId, int sizeId, string sku, int stock)
    {
        ProductId = productId;
        ColorId = colorId;
        SizeId = sizeId;
        Sku = sku;
        Stock = stock;
    }

    public int Id { get; private set; }
    public int ProductId { get; private set; }
    public int ColorId { get; private set; }
    public int SizeId { get; private set; }
    public string Sku { get; private set; }
    public int Stock { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public Product Product { get; private set; } = null!;
    public Color Color { get; private set; } = null!;
    public Size Size { get; private set; } = null!;

    /// <summary>Removes sold units; stock never goes below 0 (constitution IV).</summary>
    public void Deduct(int quantity)
    {
        EnsurePositive(quantity);
        if (quantity > Stock)
        {
            throw new DomainException($"Cannot deduct {quantity} from variant {Id}: only {Stock} in stock.");
        }

        Stock -= quantity;
    }

    /// <summary>Returns units to stock, e.g. when an order is cancelled.</summary>
    public void Restock(int quantity)
    {
        EnsurePositive(quantity);
        Stock += quantity;
    }

    private static void EnsurePositive(int quantity)
    {
        if (quantity < 1)
        {
            throw new DomainException("Quantity must be at least 1.");
        }
    }
}

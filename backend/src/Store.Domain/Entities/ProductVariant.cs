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
}

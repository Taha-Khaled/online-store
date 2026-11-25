namespace Store.Domain.Entities;

public class ProductImage
{
    public ProductImage(int productId, int? colorId, string url, int displayOrder, bool isMain)
    {
        ProductId = productId;
        ColorId = colorId;
        Url = url;
        DisplayOrder = displayOrder;
        IsMain = isMain;
    }

    public int Id { get; private set; }
    public int ProductId { get; private set; }
    public int? ColorId { get; private set; }
    public string Url { get; private set; }
    public int DisplayOrder { get; private set; }
    public bool IsMain { get; private set; }

    public Product Product { get; private set; } = null!;
    public Color? Color { get; private set; }
}

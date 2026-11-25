namespace Store.Domain.Entities;

public class Product
{
    public Product(
        int categoryId,
        string name,
        string slug,
        string description,
        decimal price,
        bool isActive,
        bool isFeatured,
        DateTime createdAt)
    {
        CategoryId = categoryId;
        Name = name;
        Slug = slug;
        Description = description;
        Price = price;
        IsActive = isActive;
        IsFeatured = isFeatured;
        CreatedAt = createdAt;
    }

    public int Id { get; private set; }
    public int CategoryId { get; private set; }
    public string Name { get; private set; }
    public string Slug { get; private set; }
    public string Description { get; private set; }
    public decimal Price { get; private set; }
    public bool IsActive { get; private set; }
    public bool IsFeatured { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public Category Category { get; private set; } = null!;
    public ICollection<ProductVariant> Variants { get; private set; } = new List<ProductVariant>();
    public ICollection<ProductImage> Images { get; private set; } = new List<ProductImage>();
}

namespace Store.Application.Products;

/// <summary>Product details (FR-040). Exact stock is never exposed; only availability and the purchasable quantity.</summary>
public sealed record ProductDetailsResponse(
    int Id,
    string Name,
    string Slug,
    string Description,
    decimal Price,
    ProductCategoryResponse Category,
    IReadOnlyList<ProductColorResponse> Colors,
    IReadOnlyList<ProductSizeResponse> Sizes,
    IReadOnlyList<VariantResponse> Variants);

public sealed record ProductCategoryResponse(string Name, string Slug, string? ParentName);

public sealed record ProductColorResponse(int Id, string Name, string HexCode, IReadOnlyList<ProductImageResponse> Images);

public sealed record ProductImageResponse(string Url, bool IsMain);

public sealed record ProductSizeResponse(int Id, string Name);

/// <summary><see cref="MaxQuantity"/> = min(stock, 5); 0 when out of stock.</summary>
public sealed record VariantResponse(int Id, int ColorId, int SizeId, string Sku, bool InStock, int MaxQuantity);

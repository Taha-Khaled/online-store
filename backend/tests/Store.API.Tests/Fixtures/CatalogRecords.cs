namespace Store.API.Tests.Fixtures;

/// <summary>Test-local shapes of the catalog responses (contracts/api.md).</summary>
public sealed record ColorItem(string Name, string HexCode);

public sealed record ProductItem(
    int Id,
    string Name,
    string Slug,
    decimal Price,
    string CategoryName,
    bool IsFeatured,
    string? MainImageUrl,
    List<ColorItem> Colors,
    bool InStock);

public sealed record ProductPage(List<ProductItem> Items, int Page, int PageSize, int TotalCount, int TotalPages);

public sealed record SizeItem(string Name);

public sealed record FilterPanel(List<ColorItem> Colors, List<SizeItem> Sizes, decimal? MinPrice, decimal? MaxPrice);

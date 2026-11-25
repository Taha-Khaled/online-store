namespace Store.Application.Products;

/// <summary>One product in the list (FR-038).</summary>
public sealed record ProductListItemResponse(
    int Id,
    string Name,
    string Slug,
    decimal Price,
    string CategoryName,
    bool IsFeatured,
    string? MainImageUrl,
    IReadOnlyList<ColorSummaryResponse> Colors,
    bool InStock);

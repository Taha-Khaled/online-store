namespace Store.Application.Products;

/// <summary>
/// Product list options (contracts/api.md "GET /api/products"). <see cref="Color"/> and
/// <see cref="Size"/> hold the repeatable <c>color</c> / <c>size</c> query values.
/// </summary>
public sealed record ProductListQuery
{
    public const int DefaultPageSize = 12;

    public string? Search { get; init; }

    public string? Category { get; init; }

    public IReadOnlyList<string>? Color { get; init; }

    public IReadOnlyList<string>? Size { get; init; }

    public decimal? MinPrice { get; init; }

    public decimal? MaxPrice { get; init; }

    public bool Featured { get; init; }

    public ProductSort Sort { get; init; } = ProductSort.Newest;

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = DefaultPageSize;
}

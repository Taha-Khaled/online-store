namespace Store.Application.Products.Responses;

/// <summary>Filter panel options (FR-041).</summary>
public sealed record ProductFiltersResponse(
    IReadOnlyList<ColorSummaryResponse> Colors,
    IReadOnlyList<SizeSummaryResponse> Sizes,
    decimal? MinPrice,
    decimal? MaxPrice);

public sealed record SizeSummaryResponse(string Name);

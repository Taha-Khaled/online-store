using Store.Domain.Entities;

namespace Store.Application.Products;

/// <summary>Composable product list filters and sort orders (research R8–R10).</summary>
public static class ProductQueryExtensions
{
    /// <summary>
    /// Case-insensitive partial match on name, description, category or parent category name
    /// (FR-032). Both sides are lowercased so the column collation does not matter; EF Core sends
    /// the term as a parameter and escapes LIKE wildcards (<c>%</c>, <c>_</c>, <c>[</c>).
    /// </summary>
    public static IQueryable<Product> ApplySearch(this IQueryable<Product> products, string? search)
    {
        var term = search?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(term))
        {
            return products;
        }

        return products.Where(product =>
            product.Name.ToLower().Contains(term)
            || product.Description.ToLower().Contains(term)
            || product.Category.Name.ToLower().Contains(term)
            || (product.Category.ParentCategory != null && product.Category.ParentCategory.Name.ToLower().Contains(term)));
    }

    /// <summary>
    /// Keeps products with one in-stock variant that has a requested color AND a requested size
    /// (FR-033, FR-035). Names are compared case-insensitively; with neither filter given, nothing
    /// is filtered.
    /// </summary>
    public static IQueryable<Product> ApplyVariantFilters(
        this IQueryable<Product> products,
        IEnumerable<string> colors,
        IEnumerable<string> sizes)
    {
        var colorNames = NormalizeNames(colors);
        var sizeNames = NormalizeNames(sizes);
        if (colorNames.Count == 0 && sizeNames.Count == 0)
        {
            return products;
        }

        var anyColor = colorNames.Count == 0;
        var anySize = sizeNames.Count == 0;

        return products.Where(product => product.Variants.Any(variant =>
            variant.Stock > 0
            && (anyColor || colorNames.Contains(variant.Color.Name.ToLower()))
            && (anySize || sizeNames.Contains(variant.Size.Name.ToLower()))));
    }

    /// <summary>Fixed sort chains, each ending with a unique tiebreaker (FR-036).</summary>
    public static IOrderedQueryable<Product> ApplySort(this IQueryable<Product> products, ProductSort sort) => sort switch
    {
        ProductSort.Newest => products.OrderByDescending(product => product.CreatedAt).ThenByDescending(product => product.Id),
        ProductSort.PriceAsc => products.OrderBy(product => product.Price).ThenBy(product => product.Name).ThenBy(product => product.Id),
        ProductSort.PriceDesc => products.OrderByDescending(product => product.Price).ThenBy(product => product.Name).ThenBy(product => product.Id),
        ProductSort.NameAsc => products.OrderBy(product => product.Name).ThenBy(product => product.Id),
        _ => throw new ArgumentOutOfRangeException(nameof(sort), sort, "Unknown product sort.")
    };

    private static List<string> NormalizeNames(IEnumerable<string> names) =>
        names
            .Select(name => name.Trim().ToLowerInvariant())
            .Where(name => name.Length > 0)
            .Distinct()
            .ToList();
}

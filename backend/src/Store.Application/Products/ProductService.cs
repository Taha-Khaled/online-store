using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Store.Application.Common;
using Store.Application.Common.Errors;
using Store.Application.Common.Validation;
using Store.Domain;
using Store.Domain.Entities;

namespace Store.Application.Products;

/// <summary>Public catalog queries; only active products are ever returned (FR-030).</summary>
public sealed class ProductService(IAppDbContext db, IValidator<ProductListQuery> listValidator)
{
    public async Task<PagedResponse<ProductListItemResponse>> ListAsync(
        ProductListQuery query,
        CancellationToken cancellationToken = default)
    {
        await listValidator.ValidateOrThrowAsync(query, cancellationToken);

        var categoryIds = await ResolveCategoryIdsAsync(query.Category, cancellationToken);
        var products = FilterProducts(query, categoryIds);

        var totalCount = await products.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(totalCount / (double)query.PageSize);
        var offset = (long)(query.Page - 1) * query.PageSize;
        if (offset >= totalCount)
        {
            return new PagedResponse<ProductListItemResponse>([], query.Page, query.PageSize, totalCount, totalPages);
        }

        var rows = await products
            .ApplySort(query.Sort)
            .Skip((int)offset)
            .Take(query.PageSize)
            .Select(product => new ProductRow(
                product.Id,
                product.Name,
                product.Slug,
                product.Price,
                product.Category.Name,
                product.IsFeatured,
                product.Images
                    .Where(image => image.IsMain)
                    .OrderBy(image => image.DisplayOrder)
                    .ThenBy(image => image.Id)
                    .Select(image => image.Url)
                    .FirstOrDefault(),
                product.Variants.Any(variant => variant.Stock > 0)))
            .ToListAsync(cancellationToken);

        var colors = await LoadColorsAsync(rows.Select(row => row.Id).ToList(), cancellationToken);
        var items = rows.Select(row => ToListItem(row, colors)).ToList();

        return new PagedResponse<ProductListItemResponse>(items, query.Page, query.PageSize, totalCount, totalPages);
    }

    /// <summary>
    /// Details of an active product by slug (FR-040). The purchasable quantity is computed in the
    /// query, so the exact stock never leaves the database.
    /// </summary>
    public async Task<ProductDetailsResponse> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        var normalized = slug.Trim().ToLowerInvariant();
        var product = await db.Products
            .AsNoTracking()
            .Where(candidate => candidate.Slug == normalized && candidate.IsActive)
            .Select(candidate => new ProductHeader(
                candidate.Id,
                candidate.Name,
                candidate.Slug,
                candidate.Description,
                candidate.Price,
                new ProductCategoryResponse(
                    candidate.Category.Name,
                    candidate.Category.Slug,
                    candidate.Category.ParentCategory != null ? candidate.Category.ParentCategory.Name : null)))
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(ErrorCodes.ProductNotFound, "Product not found.");

        var variants = await db.ProductVariants
            .AsNoTracking()
            .Where(variant => variant.ProductId == product.Id)
            .Select(variant => new VariantRow(
                variant.Id,
                variant.ColorId,
                variant.Color.Name,
                variant.Color.HexCode,
                variant.SizeId,
                variant.Size.Name,
                variant.Size.DisplayOrder,
                variant.Sku,
                variant.Stock > 0,
                variant.Stock < StoreLimits.MaxQuantityPerVariant ? variant.Stock : StoreLimits.MaxQuantityPerVariant))
            .ToListAsync(cancellationToken);

        var images = await db.ProductImages
            .AsNoTracking()
            .Where(image => image.ProductId == product.Id)
            .OrderBy(image => image.DisplayOrder)
            .ThenBy(image => image.Id)
            .Select(image => new ImageRow(image.ColorId, image.Url, image.IsMain))
            .ToListAsync(cancellationToken);

        return ToDetails(product, variants, images);
    }

    /// <summary>
    /// Colors and sizes with at least one in-stock variant on an active product in scope, plus the
    /// price range of the active products in scope (FR-041).
    /// </summary>
    public async Task<ProductFiltersResponse> GetFiltersAsync(string? category, CancellationToken cancellationToken = default)
    {
        var categoryIds = await ResolveCategoryIdsAsync(category, cancellationToken);
        var products = ActiveProducts(categoryIds);
        var inStockVariants = products.SelectMany(product => product.Variants).Where(variant => variant.Stock > 0);

        var colors = await inStockVariants
            .Select(variant => new { variant.Color.Name, variant.Color.HexCode })
            .Distinct()
            .OrderBy(color => color.Name)
            .ToListAsync(cancellationToken);

        var sizes = await inStockVariants
            .Select(variant => new { variant.Size.Name, variant.Size.DisplayOrder })
            .Distinct()
            .OrderBy(size => size.DisplayOrder)
            .ToListAsync(cancellationToken);

        var minPrice = await products.MinAsync(product => (decimal?)product.Price, cancellationToken);
        var maxPrice = await products.MaxAsync(product => (decimal?)product.Price, cancellationToken);

        return new ProductFiltersResponse(
            colors.Select(color => new ColorSummaryResponse(color.Name, color.HexCode)).ToList(),
            sizes.Select(size => new SizeSummaryResponse(size.Name)).ToList(),
            minPrice,
            maxPrice);
    }

    /// <summary>The category and its children; null when no category filter is given.</summary>
    private async Task<List<int>?> ResolveCategoryIdsAsync(string? slug, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return null;
        }

        var normalized = slug.Trim().ToLowerInvariant();
        var ids = await db.Categories
            .AsNoTracking()
            .Where(category => category.Slug == normalized
                || (category.ParentCategory != null && category.ParentCategory.Slug == normalized))
            .Select(category => category.Id)
            .ToListAsync(cancellationToken);

        return ids.Count > 0
            ? ids
            : throw new NotFoundException(ErrorCodes.CategoryNotFound, "Category not found.");
    }

    private IQueryable<Product> FilterProducts(ProductListQuery query, List<int>? categoryIds)
    {
        var products = ActiveProducts(categoryIds);

        if (query.Featured)
        {
            products = products.Where(product => product.IsFeatured);
        }

        if (query.MinPrice.HasValue)
        {
            products = products.Where(product => product.Price >= query.MinPrice.Value);
        }

        if (query.MaxPrice.HasValue)
        {
            products = products.Where(product => product.Price <= query.MaxPrice.Value);
        }

        return products
            .ApplySearch(query.Search)
            .ApplyVariantFilters(query.Color ?? [], query.Size ?? []);
    }

    private IQueryable<Product> ActiveProducts(List<int>? categoryIds)
    {
        var products = db.Products.AsNoTracking().Where(product => product.IsActive);
        return categoryIds is null
            ? products
            : products.Where(product => categoryIds.Contains(product.CategoryId));
    }

    /// <summary>Each product's variant colors, ordered by their first image (FR-039).</summary>
    private async Task<Dictionary<int, List<ColorSummaryResponse>>> LoadColorsAsync(
        List<int> productIds,
        CancellationToken cancellationToken)
    {
        var colors = await db.ProductVariants
            .AsNoTracking()
            .Where(variant => productIds.Contains(variant.ProductId))
            .Select(variant => new ProductColor(variant.ProductId, variant.ColorId, variant.Color.Name, variant.Color.HexCode))
            .Distinct()
            .ToListAsync(cancellationToken);

        var images = await db.ProductImages
            .AsNoTracking()
            .Where(image => productIds.Contains(image.ProductId) && image.ColorId != null)
            .Select(image => new ImagePosition(image.ProductId, image.ColorId, image.DisplayOrder, image.Id))
            .ToListAsync(cancellationToken);

        var firstImage = images
            .GroupBy(image => (image.ProductId, image.ColorId))
            .ToDictionary(group => group.Key, group => group.Min(image => (image.DisplayOrder, image.Id)));

        return colors
            .GroupBy(color => color.ProductId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderBy(color => firstImage.ContainsKey((color.ProductId, color.ColorId)) ? 0 : 1)
                    .ThenBy(color => firstImage.GetValueOrDefault((color.ProductId, color.ColorId)))
                    .ThenBy(color => color.Name, StringComparer.Ordinal)
                    .Select(color => new ColorSummaryResponse(color.Name, color.HexCode))
                    .ToList());
    }

    private static ProductListItemResponse ToListItem(ProductRow row, Dictionary<int, List<ColorSummaryResponse>> colors) =>
        new(
            row.Id,
            row.Name,
            row.Slug,
            row.Price,
            row.CategoryName,
            row.IsFeatured,
            row.MainImageUrl,
            colors.GetValueOrDefault(row.Id) ?? [],
            row.InStock);

    private static ProductDetailsResponse ToDetails(ProductHeader product, List<VariantRow> variants, List<ImageRow> images)
    {
        var colors = variants
            .DistinctBy(variant => variant.ColorId)
            .OrderBy(variant => FirstImageRank(images, variant.ColorId))
            .ThenBy(variant => variant.ColorName, StringComparer.Ordinal)
            .Select(variant => new ProductColorResponse(
                variant.ColorId,
                variant.ColorName,
                variant.ColorHexCode,
                images
                    .Where(image => image.ColorId == variant.ColorId)
                    .Select(image => new ProductImageResponse(image.Url, image.IsMain))
                    .ToList()))
            .ToList();

        var sizes = variants
            .DistinctBy(variant => variant.SizeId)
            .OrderBy(variant => variant.SizeDisplayOrder)
            .Select(variant => new ProductSizeResponse(variant.SizeId, variant.SizeName))
            .ToList();

        var colorOrder = colors.Select(color => color.Id).ToList();
        var orderedVariants = variants
            .OrderBy(variant => colorOrder.IndexOf(variant.ColorId))
            .ThenBy(variant => variant.SizeDisplayOrder)
            .Select(variant => new VariantResponse(
                variant.Id,
                variant.ColorId,
                variant.SizeId,
                variant.Sku,
                variant.InStock,
                variant.MaxQuantity))
            .ToList();

        return new ProductDetailsResponse(
            product.Id,
            product.Name,
            product.Slug,
            product.Description,
            product.Price,
            product.Category,
            colors,
            sizes,
            orderedVariants);
    }

    /// <summary>Position of the color's first image (images are already ordered); colors without images go last.</summary>
    private static int FirstImageRank(List<ImageRow> images, int colorId)
    {
        var index = images.FindIndex(image => image.ColorId == colorId);
        return index < 0 ? int.MaxValue : index;
    }

    private sealed record ProductHeader(
        int Id,
        string Name,
        string Slug,
        string Description,
        decimal Price,
        ProductCategoryResponse Category);

    private sealed record VariantRow(
        int Id,
        int ColorId,
        string ColorName,
        string ColorHexCode,
        int SizeId,
        string SizeName,
        int SizeDisplayOrder,
        string Sku,
        bool InStock,
        int MaxQuantity);

    private sealed record ImageRow(int? ColorId, string Url, bool IsMain);

    private sealed record ProductRow(
        int Id,
        string Name,
        string Slug,
        decimal Price,
        string CategoryName,
        bool IsFeatured,
        string? MainImageUrl,
        bool InStock);

    private sealed record ProductColor(int ProductId, int ColorId, string Name, string HexCode);

    private sealed record ImagePosition(int ProductId, int? ColorId, int DisplayOrder, int Id);
}

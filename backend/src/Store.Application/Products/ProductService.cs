using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Store.Application.Common;
using Store.Application.Common.Errors;
using Store.Application.Common.Validation;
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

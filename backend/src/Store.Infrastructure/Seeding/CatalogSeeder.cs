using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Store.Application.Common;
using Store.Domain.Entities;
using Store.Infrastructure.Seeding.Data;

namespace Store.Infrastructure.Seeding;

/// <summary>
/// Seeds categories, products, variants, images and the demo orders as one all-or-nothing group,
/// only when no product exists (spec FR-019, FR-021 to FR-023).
/// </summary>
public sealed class CatalogSeeder(
    IAppDbContext db,
    IOptions<SeedOptions> options,
    TimeProvider timeProvider,
    ILogger<CatalogSeeder> logger)
{
    private const int ProductAgeInDays = 60;
    private const string ImageUrlPrefix = "/images/products/";

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (await db.Products.AnyAsync(cancellationToken))
        {
            return;
        }

        var customer = await db.Users.SingleOrDefaultAsync(u => u.Email == AccountSeedData.DemoCustomerEmail, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Demo customer '{AccountSeedData.DemoCustomerEmail}' must exist before the catalog is seeded.");

        var seedTime = timeProvider.GetUtcNow().UtcDateTime;

        // Disposing without CommitAsync rolls everything back (research R4).
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var categories = await SeedCategoriesAsync(cancellationToken);
        var colors = await db.Colors.ToDictionaryAsync(c => c.Name, cancellationToken);
        var sizes = await db.Sizes.ToDictionaryAsync(s => s.Name, cancellationToken);
        var products = await SeedProductsAsync(categories, colors, sizes, seedTime, cancellationToken);
        await SeedOrdersAsync(customer, products, colors, sizes, seedTime, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    /// <summary>Deterministic stock for non-special variants; always 8–20 (research R9).</summary>
    public static int CalculateStock(int productNumber, int colorIndex, int sizeDisplayOrder) =>
        8 + ((productNumber * 31 + colorIndex * 7 + sizeDisplayOrder * 3) % 13);

    private async Task<Dictionary<string, Category>> SeedCategoriesAsync(CancellationToken cancellationToken)
    {
        var parents = CatalogSeedData.Categories
            .Where(c => c.ParentSlug is null)
            .ToDictionary(c => c.Slug, c => new Category(c.Name, c.Slug, null));
        db.Categories.AddRange(parents.Values);
        await db.SaveChangesAsync(cancellationToken);

        var children = CatalogSeedData.Categories
            .Where(c => c.ParentSlug is not null)
            .ToDictionary(c => c.Slug, c => new Category(c.Name, c.Slug, parents[c.ParentSlug!].Id));
        db.Categories.AddRange(children.Values);
        await db.SaveChangesAsync(cancellationToken);

        return parents.Concat(children).ToDictionary(pair => pair.Key, pair => pair.Value);
    }

    private async Task<Dictionary<string, Product>> SeedProductsAsync(
        Dictionary<string, Category> categories,
        Dictionary<string, Color> colors,
        Dictionary<string, Size> sizes,
        DateTime seedTime,
        CancellationToken cancellationToken)
    {
        var createdAt = seedTime.AddDays(-ProductAgeInDays);
        var products = new Dictionary<string, Product>();

        foreach (var seed in CatalogSeedData.Products)
        {
            var category = categories[seed.CategorySlug];
            var product = new Product(category.Id, seed.Name, seed.Slug, seed.Description, seed.Price, true, seed.IsFeatured, createdAt);
            AddVariants(product, seed, SkuCodeOf(seed.CategorySlug), colors, sizes);
            AddImages(product, seed, colors);
            products[seed.Slug] = product;
        }

        db.Products.AddRange(products.Values);
        await db.SaveChangesAsync(cancellationToken);
        return products;
    }

    private static void AddVariants(
        Product product,
        ProductSeed seed,
        string categoryCode,
        Dictionary<string, Color> colors,
        Dictionary<string, Size> sizes)
    {
        for (var colorIndex = 0; colorIndex < seed.Colors.Count; colorIndex++)
        {
            var color = colors[seed.Colors[colorIndex]];
            foreach (var size in seed.Sizes.Select(name => sizes[name]))
            {
                var sku = $"{categoryCode}-{seed.Number:D3}-{CatalogSeedData.ColorCodes[color.Name]}-{size.Name}";
                var stock = SpecialStockOf(seed.Slug, color.Name, size.Name)
                    ?? CalculateStock(seed.Number, colorIndex, size.DisplayOrder);
                product.Variants.Add(new ProductVariant(product.Id, color.Id, size.Id, sku, stock));
            }
        }
    }

    private void AddImages(Product product, ProductSeed seed, Dictionary<string, Color> colors)
    {
        foreach (var colorName in seed.Colors)
        {
            var fileName = $"{seed.Slug}-{colorName.ToLowerInvariant()}.jpg";
            WarnIfImageFileMissing(fileName);
            product.Images.Add(new ProductImage(product.Id, colors[colorName].Id, ImageUrlPrefix + fileName, 1, true));
        }
    }

    private void WarnIfImageFileMissing(string fileName)
    {
        var folder = options.Value.ImagesPath;
        if (!string.IsNullOrWhiteSpace(folder) && File.Exists(Path.Combine(folder, fileName)))
        {
            return;
        }

        logger.LogWarning("Seed image {FileName} was not found; its image row is stored anyway", fileName);
    }

    private async Task SeedOrdersAsync(
        User customer,
        Dictionary<string, Product> products,
        Dictionary<string, Color> colors,
        Dictionary<string, Size> sizes,
        DateTime seedTime,
        CancellationToken cancellationToken)
    {
        foreach (var seed in DemoOrderSeedData.Orders)
        {
            var items = seed.Items
                .Select(item => CreateOrderItem(products[item.ProductSlug], item, colors, sizes))
                .ToList();
            var subtotal = items.Sum(i => i.UnitPrice * i.Quantity);

            db.Orders.Add(Order.CreateHistorical(
                seed.OrderNumber,
                customer.Id,
                seed.Status,
                subtotal,
                DemoOrderSeedData.ShippingFee,
                subtotal + DemoOrderSeedData.ShippingFee,
                customer.FullName,
                customer.Phone,
                DemoOrderSeedData.City,
                DemoOrderSeedData.AddressLine,
                null,
                seedTime.AddDays(-seed.DaysAgo),
                items));
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static OrderItem CreateOrderItem(
        Product product,
        OrderItemSeed seed,
        Dictionary<string, Color> colors,
        Dictionary<string, Size> sizes)
    {
        var colorId = colors[seed.Color].Id;
        var sizeId = sizes[seed.Size].Id;
        var variant = product.Variants.Single(v => v.ColorId == colorId && v.SizeId == sizeId);
        return new OrderItem(variant.Id, product.Name, seed.Color, seed.Size, product.Price, seed.Quantity);
    }

    private static int? SpecialStockOf(string productSlug, string color, string size) =>
        CatalogSeedData.SpecialStock
            .FirstOrDefault(s => s.ProductSlug == productSlug && s.Color == color && s.Size == size)
            ?.Stock;

    private static string SkuCodeOf(string categorySlug) =>
        CatalogSeedData.Categories.Single(c => c.Slug == categorySlug).SkuCode!;
}

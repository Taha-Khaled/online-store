using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shouldly;
using Store.Domain.Entities;
using Store.Domain.Enums;
using Store.Infrastructure.Persistence;
using Store.Infrastructure.Seeding;
using Store.Infrastructure.Tests.Fixtures;

namespace Store.Infrastructure.Tests;

/// <summary>User Story 2: the store never opens empty. Expected values come from spec.md.</summary>
public class SeedingTests : IAsyncLifetime
{
    // spec.md "Products": name, price, featured, category path.
    private static readonly (string Name, decimal Price, bool Featured, string Category)[] ExpectedProducts =
    [
        ("Classic Cotton T-Shirt", 350m, true, "Men / T-Shirts"),
        ("Graphic Oversized Tee", 450m, false, "Men / T-Shirts"),
        ("Oxford Button-Down Shirt", 750m, true, "Men / Shirts"),
        ("Linen Summer Shirt", 850m, false, "Men / Shirts"),
        ("Slim Fit Chinos", 900m, false, "Men / Pants"),
        ("Classic Denim Jeans", 1100m, true, "Men / Pants"),
        ("Floral Midi Dress", 1250m, true, "Women / Dresses"),
        ("Casual Shirt Dress", 1050m, false, "Women / Dresses"),
        ("Basic Ribbed Top", 300m, false, "Women / Tops"),
        ("Satin Blouse", 650m, false, "Women / Tops"),
        ("Wide Leg Trousers", 950m, true, "Women / Pants"),
        ("High Waist Jeans", 1050m, false, "Women / Pants"),
        ("Everyday White Sneakers", 1600m, true, "Shoes / Sneakers"),
        ("Running Shoes", 1900m, false, "Shoes / Sneakers"),
        ("Leather Derby Shoes", 2200m, false, "Shoes / Formal"),
        ("Classic Loafers", 1800m, false, "Shoes / Formal"),
        ("Leather Sandals", 700m, false, "Shoes / Sandals"),
        ("Everyday Slides", 400m, false, "Shoes / Sandals")
    ];

    // spec.md "Special stock values".
    private static readonly (string Product, string Color, string Size, int Stock)[] ExpectedSpecialStock =
    [
        ("Classic Cotton T-Shirt", "Black", "M", 1),
        ("Classic Cotton T-Shirt", "White", "L", 0),
        ("Floral Midi Dress", "Red", "S", 2),
        ("Everyday White Sneakers", "White", "42", 0),
        ("Everyday White Sneakers", "White", "43", 3),
        ("Leather Derby Shoes", "Brown", "44", 1)
    ];

    // spec.md "Demo orders".
    private static readonly (string Number, int DaysAgo, OrderStatus Status, decimal Subtotal, decimal Total)[] ExpectedOrders =
    [
        ("ORD-0001", 28, OrderStatus.Delivered, 1800m, 1850m),
        ("ORD-0002", 24, OrderStatus.Delivered, 1600m, 1650m),
        ("ORD-0003", 20, OrderStatus.Cancelled, 1250m, 1300m),
        ("ORD-0004", 15, OrderStatus.Delivered, 1650m, 1700m),
        ("ORD-0005", 9, OrderStatus.Shipped, 2200m, 2250m),
        ("ORD-0006", 5, OrderStatus.Confirmed, 1850m, 1900m),
        ("ORD-0007", 2, OrderStatus.Pending, 850m, 900m),
        ("ORD-0008", 0, OrderStatus.Pending, 700m, 750m)
    ];

    private static readonly (string Order, string Product, string Color, string Size, decimal UnitPrice, int Quantity)[] ExpectedOrderItems =
    [
        ("ORD-0001", "Classic Denim Jeans", "Blue", "M", 1100m, 1),
        ("ORD-0001", "Classic Cotton T-Shirt", "White", "M", 350m, 2),
        ("ORD-0002", "Everyday White Sneakers", "White", "41", 1600m, 1),
        ("ORD-0003", "Floral Midi Dress", "Navy", "M", 1250m, 1),
        ("ORD-0004", "Oxford Button-Down Shirt", "White", "L", 750m, 1),
        ("ORD-0004", "Slim Fit Chinos", "Beige", "L", 900m, 1),
        ("ORD-0005", "Leather Derby Shoes", "Black", "42", 2200m, 1),
        ("ORD-0006", "Basic Ribbed Top", "Black", "S", 300m, 3),
        ("ORD-0006", "Wide Leg Trousers", "Black", "S", 950m, 1),
        ("ORD-0007", "Graphic Oversized Tee", "Gray", "L", 450m, 1),
        ("ORD-0007", "Everyday Slides", "Black", "42", 400m, 1),
        ("ORD-0008", "Classic Cotton T-Shirt", "Black", "L", 350m, 2)
    ];

    private readonly TestDatabase _db = new();

    public async Task InitializeAsync()
    {
        await using var context = _db.CreateContext();
        await context.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _db.DisposeAsync();

    [Fact]
    public async Task Seed_FreshDatabase_CreatesExactCounts()
    {
        await SeedAsync(Seeders.DefaultOptions());

        var counts = await CountRowsAsync();

        counts.ShouldBe(new RowCounts(2, 12, 18, 177, 37, 8, 12));
    }

    [Fact]
    public async Task Seed_Products_MatchSpecPriceFeaturedAndCategory()
    {
        await SeedAsync(Seeders.DefaultOptions());
        await using var context = _db.CreateContext();

        var products = await context.Products
            .Select(p => new { p.Name, p.Price, p.IsFeatured, p.IsActive, Category = p.Category.ParentCategory!.Name + " / " + p.Category.Name })
            .ToListAsync();

        products.Select(p => (p.Name, p.Price, p.IsFeatured, p.Category)).ShouldBe(ExpectedProducts, ignoreOrder: true);
        products.ShouldAllBe(p => p.IsActive);
    }

    [Fact]
    public async Task Seed_Variants_HaveSkuFormatSpecialStockAndNormalRange()
    {
        await SeedAsync(Seeders.DefaultOptions());
        await using var context = _db.CreateContext();

        var variants = await context.ProductVariants
            .Select(v => new { Product = v.Product.Name, Color = v.Color.Name, Size = v.Size.Name, v.Sku, v.Stock })
            .ToListAsync();

        variants.Single(v => v.Product == "Classic Cotton T-Shirt" && v.Color == "Black" && v.Size == "M").Sku.ShouldBe("MTS-001-BLK-M");
        variants.Single(v => v.Product == "Everyday White Sneakers" && v.Size == "42").Sku.ShouldBe("SSN-013-WHT-42");

        foreach (var (product, color, size, stock) in ExpectedSpecialStock)
        {
            variants.Single(v => v.Product == product && v.Color == color && v.Size == size).Stock.ShouldBe(stock);
        }

        var normal = variants.Where(v => !ExpectedSpecialStock.Any(s => s.Product == v.Product && s.Color == v.Color && s.Size == v.Size));
        normal.Count().ShouldBe(171);
        normal.ShouldAllBe(v => v.Stock >= 8 && v.Stock <= 20);
    }

    [Fact]
    public async Task Seed_Orders_MatchSpecTotalsStatusesAndDates()
    {
        await SeedAsync(Seeders.DefaultOptions());
        await using var context = _db.CreateContext();

        var orders = await context.Orders.Include(o => o.User).ToListAsync();

        foreach (var (number, daysAgo, status, subtotal, total) in ExpectedOrders)
        {
            var order = orders.Single(o => o.OrderNumber == number);
            order.Status.ShouldBe(status);
            order.Subtotal.ShouldBe(subtotal);
            order.ShippingFee.ShouldBe(50m);
            order.Total.ShouldBe(total);
            order.CreatedAt.ShouldBe(Seeders.SeedTime.UtcDateTime.AddDays(-daysAgo));
            order.UpdatedAt.ShouldBe(order.CreatedAt);
            order.User.Email.ShouldBe("customer@store.local");
            order.City.ShouldBe("Cairo");
        }
    }

    [Fact]
    public async Task Seed_OrderItems_StoreSnapshotsMatchingSpec()
    {
        await SeedAsync(Seeders.DefaultOptions());
        await using var context = _db.CreateContext();

        var items = await context.OrderItems
            .Select(i => new { i.Order.OrderNumber, i.ProductName, i.ColorName, i.SizeName, i.UnitPrice, i.Quantity, Variant = i.ProductVariant.Product.Name })
            .ToListAsync();

        items.Select(i => (i.OrderNumber, i.ProductName, i.ColorName, i.SizeName, i.UnitPrice, i.Quantity))
            .ShouldBe(ExpectedOrderItems, ignoreOrder: true);
        items.ShouldAllBe(i => i.Variant == i.ProductName);
    }

    [Fact]
    public async Task Seed_Passwords_AreStoredAsVerifiableHashes()
    {
        await SeedAsync(Seeders.DefaultOptions());
        await using var context = _db.CreateContext();
        var hasher = new PasswordHasher<User>();

        var admin = await context.Users.SingleAsync(u => u.Email == "admin@store.local");
        var customer = await context.Users.SingleAsync(u => u.Email == "customer@store.local");

        admin.Role.ShouldBe(Role.Admin);
        customer.Role.ShouldBe(Role.Customer);
        admin.PasswordHash.ShouldNotBe(Seeders.AdminPassword);
        hasher.VerifyHashedPassword(admin, admin.PasswordHash, Seeders.AdminPassword).ShouldNotBe(PasswordVerificationResult.Failed);
        hasher.VerifyHashedPassword(customer, customer.PasswordHash, Seeders.CustomerPassword).ShouldNotBe(PasswordVerificationResult.Failed);
    }

    [Fact]
    public async Task Seed_RunTwice_ChangesNothing()
    {
        await SeedAsync(Seeders.DefaultOptions());
        var firstCounts = await CountRowsAsync();
        var firstStock = await ReadStockBySkuAsync(_db);

        await SeedAsync(Seeders.DefaultOptions());

        (await CountRowsAsync()).ShouldBe(firstCounts);
        (await ReadStockBySkuAsync(_db)).ShouldBe(firstStock);
    }

    [Fact]
    public async Task Seed_MissingAdminPassword_ThrowsNamingKeyAndCreatesNoUser()
    {
        var options = Seeders.DefaultOptions();
        options.AdminPassword = null;

        var error = await Should.ThrowAsync<InvalidOperationException>(() => SeedAsync(options));

        error.Message.ShouldContain("Seed:AdminPassword");
        (await CountRowsAsync()).ShouldBe(new RowCounts(0, 0, 0, 0, 0, 0, 0));
    }

    [Fact]
    public async Task Seed_MissingCustomerPassword_ThrowsNamingKeyAndCreatesNoUser()
    {
        var options = Seeders.DefaultOptions();
        options.CustomerPassword = " ";

        var error = await Should.ThrowAsync<InvalidOperationException>(() => SeedAsync(options));

        error.Message.ShouldContain("Seed:CustomerPassword");
        (await CountRowsAsync()).Users.ShouldBe(0);
    }

    [Fact]
    public async Task Seed_AdminAlreadyExists_CreatesOnlyCustomer()
    {
        await using (var context = _db.CreateContext())
        {
            context.Users.Add(new User("Store Admin", "admin@store.local", "01000000001", "existing-hash", Role.Admin, DateTime.UtcNow));
            await context.SaveChangesAsync();
        }

        var options = Seeders.DefaultOptions();
        options.AdminPassword = null;
        await SeedAsync(options);

        await using var check = _db.CreateContext();
        (await check.Users.CountAsync()).ShouldBe(2);
        (await check.Users.SingleAsync(u => u.Email == "admin@store.local")).PasswordHash.ShouldBe("existing-hash");
    }

    [Fact]
    public async Task Seed_TwoFreshDatabases_ProduceIdenticalStock()
    {
        var other = new TestDatabase();
        try
        {
            await using (var context = other.CreateContext())
            {
                await context.Database.MigrateAsync();
                await Seeders.CreateDatabaseSeeder(context, Seeders.DefaultOptions()).SeedAsync();
            }

            await SeedAsync(Seeders.DefaultOptions());

            var first = await ReadStockBySkuAsync(_db);
            var second = await ReadStockBySkuAsync(other);
            first.Count.ShouldBe(177);
            second.ShouldBe(first);
        }
        finally
        {
            await other.DisposeAsync();
        }
    }

    [Fact]
    public async Task Seed_ExistingCategorySlugWithoutProducts_RollsBackWholeCatalogGroup()
    {
        await using (var context = _db.CreateContext())
        {
            context.Categories.Add(new Category("Men", "men", null));
            await context.SaveChangesAsync();
        }

        await Should.ThrowAsync<DbUpdateException>(() => SeedAsync(Seeders.DefaultOptions()));

        var counts = await CountRowsAsync();
        counts.ShouldBe(new RowCounts(2, 1, 0, 0, 0, 0, 0));
    }

    [Fact]
    public async Task Seed_CatalogWithoutDemoCustomer_ThrowsAndSavesNothing()
    {
        await using (var context = _db.CreateContext())
        {
            var catalogSeeder = Seeders.CreateCatalogSeeder(context, Seeders.DefaultOptions());

            var error = await Should.ThrowAsync<InvalidOperationException>(() => catalogSeeder.SeedAsync());

            error.Message.ShouldContain("customer@store.local");
        }

        (await CountRowsAsync()).ShouldBe(new RowCounts(0, 0, 0, 0, 0, 0, 0));
    }

    [Fact]
    public async Task Seed_ProductsAlreadyExist_SkipsCatalogGroup()
    {
        await using (var context = _db.CreateContext())
        {
            var category = new Category("Admin Category", "admin-category", null);
            context.Categories.Add(category);
            await context.SaveChangesAsync();
            context.Products.Add(new Product(category.Id, "Admin Product", "admin-product", "Added by the admin.", 100m, true, false, DateTime.UtcNow));
            await context.SaveChangesAsync();
        }

        await SeedAsync(Seeders.DefaultOptions());

        (await CountRowsAsync()).ShouldBe(new RowCounts(2, 1, 1, 0, 0, 0, 0));
    }

    [Fact]
    public async Task Seed_Disabled_DoesNothingEvenWithoutPasswords()
    {
        await SeedAsync(new SeedOptions { Enabled = false });

        (await CountRowsAsync()).ShouldBe(new RowCounts(0, 0, 0, 0, 0, 0, 0));
    }

    [Fact]
    public async Task Seed_ImagesFolderMissing_WarnsPerFileByNameAndStillStoresRows()
    {
        var logger = new ListLogger<CatalogSeeder>();
        var options = Seeders.DefaultOptions();
        options.ImagesPath = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}");

        await SeedAsync(options, logger);

        var warnings = logger.Entries.Where(e => e.Level == LogLevel.Warning).Select(e => e.Message).ToList();
        warnings.Count.ShouldBe(37);
        warnings.ShouldContain(w => w.Contains("classic-cotton-t-shirt-black.jpg"));
        warnings.ShouldAllBe(w => !w.Contains(options.ImagesPath));
        (await CountRowsAsync()).Images.ShouldBe(37);
    }

    [Fact]
    public async Task Seed_ImagesFolderWithOneFile_WarnsOnlyForMissingFiles()
    {
        var folder = Directory.CreateTempSubdirectory("store-seed-images-").FullName;
        try
        {
            await File.WriteAllBytesAsync(Path.Combine(folder, "classic-cotton-t-shirt-black.jpg"), [0xFF, 0xD8]);
            var logger = new ListLogger<CatalogSeeder>();
            var options = Seeders.DefaultOptions();
            options.ImagesPath = folder;

            await SeedAsync(options, logger);

            var warnings = logger.Entries.Where(e => e.Level == LogLevel.Warning).Select(e => e.Message).ToList();
            warnings.Count.ShouldBe(36);
            warnings.ShouldNotContain(w => w.Contains("classic-cotton-t-shirt-black.jpg"));
            (await CountRowsAsync()).Images.ShouldBe(37);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    private async Task SeedAsync(SeedOptions options, ListLogger<CatalogSeeder>? logger = null)
    {
        await using var context = _db.CreateContext();
        await Seeders.CreateDatabaseSeeder(context, options, logger).SeedAsync();
    }

    private async Task<RowCounts> CountRowsAsync()
    {
        await using var context = _db.CreateContext();
        return new RowCounts(
            await context.Users.CountAsync(),
            await context.Categories.CountAsync(),
            await context.Products.CountAsync(),
            await context.ProductVariants.CountAsync(),
            await context.ProductImages.CountAsync(),
            await context.Orders.CountAsync(),
            await context.OrderItems.CountAsync());
    }

    private static async Task<Dictionary<string, int>> ReadStockBySkuAsync(TestDatabase database)
    {
        await using AppDbContext context = database.CreateContext();
        return await context.ProductVariants.ToDictionaryAsync(v => v.Sku, v => v.Stock);
    }

    private sealed record RowCounts(int Users, int Categories, int Products, int Variants, int Images, int Orders, int OrderItems);
}

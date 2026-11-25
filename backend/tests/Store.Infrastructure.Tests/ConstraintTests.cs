using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Store.Domain.Entities;
using Store.Domain.Enums;
using Store.Infrastructure.Tests.Fixtures;

namespace Store.Infrastructure.Tests;

/// <summary>
/// User Story 3: SQL Server itself rejects invalid data. Statements go straight to the database
/// (ExecuteUpdate/ExecuteDelete or fresh contexts) so EF Core cannot hide a missing rule (research R7).
/// Runs against the seeded store: "Classic Denim Jeans" is ordered, "Running Shoes" never is.
/// </summary>
public class ConstraintTests : IAsyncLifetime
{
    private const string OrderedProduct = "classic-denim-jeans";
    private const string NeverOrderedProduct = "running-shoes";

    private readonly TestDatabase _db = new();

    public async Task InitializeAsync()
    {
        await using var context = _db.CreateContext();
        await context.Database.MigrateAsync();
        await Seeders.CreateDatabaseSeeder(context, Seeders.DefaultOptions()).SeedAsync();
    }

    public Task DisposeAsync() => _db.DisposeAsync();

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    public async Task VariantStock_NegativeRejected_ZeroAccepted(int stock, bool accepted)
    {
        var variantId = await VariantIdAsync(NeverOrderedProduct, "Black", "42");

        await ShouldMatchAsync(accepted, async () =>
        {
            await using var context = _db.CreateContext();
            await context.ProductVariants.Where(v => v.Id == variantId)
                .ExecuteUpdateAsync(s => s.SetProperty(v => v.Stock, stock));
        });
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(5, true)]
    [InlineData(6, false)]
    public async Task CartItemQuantity_MustBeBetweenOneAndFive(int quantity, bool accepted)
    {
        var adminId = await UserIdAsync("admin@store.local");
        var variantId = await VariantIdAsync(NeverOrderedProduct, "Black", "42");

        await ShouldMatchAsync(accepted, async () =>
        {
            await using var context = _db.CreateContext();
            context.CartItems.Add(new CartItem(adminId, variantId, quantity, DateTime.UtcNow));
            await context.SaveChangesAsync();
        });
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(5, true)]
    [InlineData(6, false)]
    public async Task OrderItemQuantity_MustBeBetweenOneAndFive(int quantity, bool accepted)
    {
        await ShouldMatchAsync(accepted, async () =>
        {
            await using var context = _db.CreateContext();
            await context.OrderItems.Where(i => i.Order.OrderNumber == "ORD-0002")
                .ExecuteUpdateAsync(s => s.SetProperty(i => i.Quantity, quantity));
        });
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    [InlineData(0.01, true)]
    public async Task ProductPrice_MustBeGreaterThanZero(double price, bool accepted)
    {
        await ShouldMatchAsync(accepted, async () =>
        {
            await using var context = _db.CreateContext();
            await context.Products.Where(p => p.Slug == NeverOrderedProduct)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Price, (decimal)price));
        });
    }

    [Fact]
    public async Task DuplicateProductColorSize_IsRejected()
    {
        var existing = await VariantAsync(NeverOrderedProduct, "Black", "42");

        await ShouldBeRejectedAsync(async () =>
        {
            await using var context = _db.CreateContext();
            context.ProductVariants.Add(new ProductVariant(existing.ProductId, existing.ColorId, existing.SizeId, "SSN-014-BLK-42-DUP", 5));
            await context.SaveChangesAsync();
        });
    }

    [Fact]
    public async Task DuplicateSku_IsRejected()
    {
        // Running Shoes has no size 38, so only the SKU clashes.
        var existing = await VariantAsync(NeverOrderedProduct, "Black", "42");
        var size38Id = await SizeIdAsync("38");

        await ShouldBeRejectedAsync(async () =>
        {
            await using var context = _db.CreateContext();
            context.ProductVariants.Add(new ProductVariant(existing.ProductId, existing.ColorId, size38Id, existing.Sku, 5));
            await context.SaveChangesAsync();
        });
    }

    [Fact]
    public async Task DuplicateEmail_IsRejected()
    {
        await ShouldBeRejectedAsync(async () =>
        {
            await using var context = _db.CreateContext();
            context.Users.Add(new User("Another Admin", "admin@store.local", "01000000009", "hash", Role.Admin, DateTime.UtcNow));
            await context.SaveChangesAsync();
        });
    }

    [Fact]
    public async Task DuplicateCategorySlug_IsRejected()
    {
        await ShouldBeRejectedAsync(async () =>
        {
            await using var context = _db.CreateContext();
            context.Categories.Add(new Category("Men Again", "men", null));
            await context.SaveChangesAsync();
        });
    }

    [Fact]
    public async Task DuplicateProductSlug_IsRejected()
    {
        var categoryId = await CategoryIdAsync("shoes-sneakers");

        await ShouldBeRejectedAsync(async () =>
        {
            await using var context = _db.CreateContext();
            context.Products.Add(new Product(categoryId, "Running Shoes 2", NeverOrderedProduct, "Duplicate slug.", 100m, true, false, DateTime.UtcNow));
            await context.SaveChangesAsync();
        });
    }

    [Fact]
    public async Task DuplicateOrderNumber_IsRejected()
    {
        var customerId = await UserIdAsync("customer@store.local");

        await ShouldBeRejectedAsync(async () =>
        {
            await using var context = _db.CreateContext();
            context.Orders.Add(Order.CreateHistorical(
                "ORD-0001", customerId, OrderStatus.Pending, 0m, 50m, 50m,
                "Demo Customer", "01000000002", "Cairo", "Street", null, DateTime.UtcNow, []));
            await context.SaveChangesAsync();
        });
    }

    [Fact]
    public async Task DuplicateCartUserAndVariant_IsRejected()
    {
        var adminId = await UserIdAsync("admin@store.local");
        var variantId = await VariantIdAsync(NeverOrderedProduct, "Black", "42");
        await using (var context = _db.CreateContext())
        {
            context.CartItems.Add(new CartItem(adminId, variantId, 1, DateTime.UtcNow));
            await context.SaveChangesAsync();
        }

        await ShouldBeRejectedAsync(async () =>
        {
            await using var context = _db.CreateContext();
            context.CartItems.Add(new CartItem(adminId, variantId, 2, DateTime.UtcNow));
            await context.SaveChangesAsync();
        });
    }

    [Fact]
    public async Task DuplicateOrderAndVariant_IsRejected()
    {
        // ORD-0001 has two lines; pointing the T-shirt line at the jeans variant duplicates it.
        var jeansVariantId = await VariantIdAsync(OrderedProduct, "Blue", "M");

        await ShouldBeRejectedAsync(async () =>
        {
            await using var context = _db.CreateContext();
            await context.OrderItems
                .Where(i => i.Order.OrderNumber == "ORD-0001" && i.ProductVariantId != jeansVariantId)
                .ExecuteUpdateAsync(s => s.SetProperty(i => i.ProductVariantId, jeansVariantId));
        });
    }

    [Fact]
    public async Task DeleteOrderedVariant_IsRejectedAndNothingRemoved()
    {
        var variantId = await VariantIdAsync(OrderedProduct, "Blue", "M");

        await ShouldBeRejectedAsync(async () =>
        {
            await using var context = _db.CreateContext();
            await context.ProductVariants.Where(v => v.Id == variantId).ExecuteDeleteAsync();
        });

        await using var check = _db.CreateContext();
        (await check.ProductVariants.AnyAsync(v => v.Id == variantId)).ShouldBeTrue();
    }

    [Fact]
    public async Task DeleteOrderedProduct_IsRejectedAndNothingRemoved()
    {
        await ShouldBeRejectedAsync(async () =>
        {
            await using var context = _db.CreateContext();
            await context.Products.Where(p => p.Slug == OrderedProduct).ExecuteDeleteAsync();
        });

        await using var check = _db.CreateContext();
        (await check.Products.CountAsync(p => p.Slug == OrderedProduct)).ShouldBe(1);
        (await check.ProductVariants.CountAsync(v => v.Product.Slug == OrderedProduct)).ShouldBe(10);
        (await check.ProductImages.CountAsync(i => i.Product.Slug == OrderedProduct)).ShouldBe(2);
    }

    [Fact]
    public async Task DeleteNeverOrderedProduct_RemovesItsVariantsAndImages()
    {
        await using (var context = _db.CreateContext())
        {
            (await context.Products.Where(p => p.Slug == NeverOrderedProduct).ExecuteDeleteAsync()).ShouldBe(1);
        }

        await using var check = _db.CreateContext();
        (await check.ProductVariants.CountAsync()).ShouldBe(177 - 14);
        (await check.ProductImages.CountAsync()).ShouldBe(37 - 2);
    }

    [Fact]
    public async Task DeleteOrder_RemovesItsItems()
    {
        await using (var context = _db.CreateContext())
        {
            (await context.Orders.Where(o => o.OrderNumber == "ORD-0001").ExecuteDeleteAsync()).ShouldBe(1);
        }

        await using var check = _db.CreateContext();
        (await check.OrderItems.CountAsync()).ShouldBe(12 - 2);
    }

    [Theory]
    [InlineData("men-t-shirts")]
    [InlineData("men")]
    public async Task DeleteCategoryWithProductsOrChildren_IsRejected(string slug)
    {
        await ShouldBeRejectedAsync(async () =>
        {
            await using var context = _db.CreateContext();
            await context.Categories.Where(c => c.Slug == slug).ExecuteDeleteAsync();
        });

        await using var check = _db.CreateContext();
        (await check.Categories.AnyAsync(c => c.Slug == slug)).ShouldBeTrue();
    }

    [Fact]
    public async Task DeleteUserWithOrders_IsRejected()
    {
        await ShouldBeRejectedAsync(async () =>
        {
            await using var context = _db.CreateContext();
            await context.Users.Where(u => u.Email == "customer@store.local").ExecuteDeleteAsync();
        });

        await using var check = _db.CreateContext();
        (await check.Users.AnyAsync(u => u.Email == "customer@store.local")).ShouldBeTrue();
    }

    [Fact]
    public async Task DeleteUser_RemovesTheirCartItems()
    {
        var adminId = await UserIdAsync("admin@store.local");
        await AddCartItemAsync(adminId, await VariantIdAsync(NeverOrderedProduct, "Black", "42"));

        await using (var context = _db.CreateContext())
        {
            (await context.Users.Where(u => u.Id == adminId).ExecuteDeleteAsync()).ShouldBe(1);
        }

        await using var check = _db.CreateContext();
        (await check.CartItems.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task DeleteVariant_RemovesItFromCarts()
    {
        var adminId = await UserIdAsync("admin@store.local");
        var variantId = await VariantIdAsync(NeverOrderedProduct, "Black", "42");
        await AddCartItemAsync(adminId, variantId);

        await using (var context = _db.CreateContext())
        {
            (await context.ProductVariants.Where(v => v.Id == variantId).ExecuteDeleteAsync()).ShouldBe(1);
        }

        await using var check = _db.CreateContext();
        (await check.CartItems.CountAsync()).ShouldBe(0);
    }

    private static async Task ShouldMatchAsync(bool accepted, Func<Task> write)
    {
        if (accepted)
        {
            await write();
            return;
        }

        await ShouldBeRejectedAsync(write);
    }

    private static async Task ShouldBeRejectedAsync(Func<Task> write)
    {
        var error = await Should.ThrowAsync<Exception>(write);
        (error is DbUpdateException or DbException).ShouldBeTrue($"Expected a database rejection but got {error.GetType().Name}.");
    }

    private async Task AddCartItemAsync(int userId, int variantId)
    {
        await using var context = _db.CreateContext();
        context.CartItems.Add(new CartItem(userId, variantId, 1, DateTime.UtcNow));
        await context.SaveChangesAsync();
    }

    private async Task<ProductVariant> VariantAsync(string productSlug, string color, string size)
    {
        await using var context = _db.CreateContext();
        return await context.ProductVariants.AsNoTracking()
            .SingleAsync(v => v.Product.Slug == productSlug && v.Color.Name == color && v.Size.Name == size);
    }

    private async Task<int> VariantIdAsync(string productSlug, string color, string size) =>
        (await VariantAsync(productSlug, color, size)).Id;

    private async Task<int> UserIdAsync(string email)
    {
        await using var context = _db.CreateContext();
        return await context.Users.Where(u => u.Email == email).Select(u => u.Id).SingleAsync();
    }

    private async Task<int> SizeIdAsync(string name)
    {
        await using var context = _db.CreateContext();
        return await context.Sizes.Where(s => s.Name == name).Select(s => s.Id).SingleAsync();
    }

    private async Task<int> CategoryIdAsync(string slug)
    {
        await using var context = _db.CreateContext();
        return await context.Categories.Where(c => c.Slug == slug).Select(c => c.Id).SingleAsync();
    }
}

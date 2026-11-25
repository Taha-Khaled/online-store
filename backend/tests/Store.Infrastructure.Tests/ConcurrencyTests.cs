using Microsoft.EntityFrameworkCore;
using Shouldly;
using Store.Domain.Entities;
using Store.Infrastructure.Persistence;
using Store.Infrastructure.Tests.Fixtures;

namespace Store.Infrastructure.Tests;

/// <summary>
/// User Story 4: two sessions updating the same variant's stock never silently overwrite each
/// other; the rowversion token turns the second save into a detected conflict (SC-004).
/// </summary>
public class ConcurrencyTests : IAsyncLifetime
{
    private readonly TestDatabase _db = new();

    public async Task InitializeAsync()
    {
        await using var context = _db.CreateContext();
        await context.Database.MigrateAsync();
        await Seeders.CreateDatabaseSeeder(context, Seeders.DefaultOptions()).SeedAsync();
    }

    public Task DisposeAsync() => _db.DisposeAsync();

    [Fact]
    public async Task ConcurrentStockUpdates_FirstSucceeds_SecondIsDetectedAsConflict()
    {
        await using var sessionA = _db.CreateContext();
        await using var sessionB = _db.CreateContext();
        var variantA = await LoadVariantAsync(sessionA);
        var variantB = await LoadVariantAsync(sessionB);
        var originalVersion = variantA.RowVersion;
        variantB.RowVersion.ShouldBe(originalVersion);

        SetStock(sessionA, variantA, 7);
        SetStock(sessionB, variantB, 3);

        await sessionA.SaveChangesAsync();
        await Should.ThrowAsync<DbUpdateConcurrencyException>(() => sessionB.SaveChangesAsync());

        await using var check = _db.CreateContext();
        var stored = await LoadVariantAsync(check);
        stored.Stock.ShouldBe(7);
        stored.RowVersion.ShouldNotBe(originalVersion);
    }

    [Fact]
    public async Task StockUpdate_AfterReloadingCurrentVersion_Succeeds()
    {
        await using (var sessionA = _db.CreateContext())
        {
            SetStock(sessionA, await LoadVariantAsync(sessionA), 7);
            await sessionA.SaveChangesAsync();
        }

        await using (var sessionB = _db.CreateContext())
        {
            SetStock(sessionB, await LoadVariantAsync(sessionB), 6);
            await sessionB.SaveChangesAsync();
        }

        await using var check = _db.CreateContext();
        (await LoadVariantAsync(check)).Stock.ShouldBe(6);
    }

    private static Task<ProductVariant> LoadVariantAsync(AppDbContext context) =>
        context.ProductVariants.SingleAsync(v => v.Sku == "MTS-001-BLK-M");

    // Stock has a private setter; later stock services change it through tracked entities as well.
    private static void SetStock(AppDbContext context, ProductVariant variant, int stock) =>
        context.Entry(variant).Property(v => v.Stock).CurrentValue = stock;
}

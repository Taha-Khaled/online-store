using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Store.Infrastructure.Tests.Fixtures;

namespace Store.Infrastructure.Tests;

/// <summary>
/// User Story 5: dbo.sp_GetDashboardStats against the seeded store
/// (specs/001-database/contracts/sp_GetDashboardStats.md).
/// </summary>
public class DashboardStatsTests : IAsyncLifetime
{
    private static readonly DateTime SeedTime = Seeders.SeedTime.UtcDateTime;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly string[] ExpectedLowStock =
    [
        "Classic Cotton T-Shirt/Black/M/MTS-001-BLK-M/1",
        "Leather Derby Shoes/Brown/44/SFM-015-BRN-44/1",
        "Floral Midi Dress/Red/S/WDR-007-RED-S/2",
        "Everyday White Sneakers/White/43/SSN-013-WHT-43/3"
    ];

    private readonly TestDatabase _db = new();

    public async Task InitializeAsync()
    {
        await using var context = _db.CreateContext();
        await context.Database.MigrateAsync();
        await Seeders.CreateDatabaseSeeder(context, Seeders.DefaultOptions()).SeedAsync();
    }

    public Task DisposeAsync() => _db.DisposeAsync();

    [Fact]
    public async Task Procedure_ExistsAfterMigration()
    {
        await using var context = _db.CreateContext();

        var procedures = await context.Database
            .SqlQuery<string>($"SELECT name AS [Value] FROM sys.procedures")
            .ToListAsync();

        procedures.ShouldBe(["sp_GetDashboardStats"]);
    }

    [Fact]
    public async Task Last30Days_Threshold3_ReturnsContractValues()
    {
        var stats = await GetStatsAsync(SeedTime.AddDays(-30), SeedTime.AddMinutes(1), 3);

        stats.TotalRevenue.ShouldBe(5200m);
        stats.TotalOrders.ShouldBe(8);
        stats.OrdersByStatus.ShouldBe(new OrdersByStatus(2, 1, 1, 3, 1));
        stats.BestSellers.Select(b => (b.ProductName, b.UnitsSold)).ShouldBe(
        [
            ("Classic Cotton T-Shirt", 4),
            ("Basic Ribbed Top", 3),
            ("Classic Denim Jeans", 1),
            ("Everyday Slides", 1),
            ("Everyday White Sneakers", 1)
        ]);
        stats.LowStockVariants.Select(Describe).ShouldBe(ExpectedLowStock);
    }

    [Fact]
    public async Task RangeWithoutOrders_ReturnsZerosEmptyBestSellersAndUnchangedLowStock()
    {
        var json = await GetJsonAsync(SeedTime.AddDays(1), SeedTime.AddDays(2), 3);
        var stats = Deserialize(json);

        json.ShouldContain("\"bestSellers\":[]");
        stats.TotalRevenue.ShouldBe(0m);
        stats.TotalOrders.ShouldBe(0);
        stats.OrdersByStatus.ShouldBe(new OrdersByStatus(0, 0, 0, 0, 0));
        stats.BestSellers.ShouldBeEmpty();
        stats.LowStockVariants.Select(Describe).ShouldBe(ExpectedLowStock);
    }

    [Fact]
    public async Task ThresholdOmitted_DefaultsToThree()
    {
        await using var context = _db.CreateContext();
        var from = SeedTime.AddDays(-30);
        var to = SeedTime.AddMinutes(1);

        var json = (await context.Database
            .SqlQuery<string>($"EXEC dbo.sp_GetDashboardStats @FromUtc = {from}, @ToUtc = {to}")
            .ToListAsync()).Single();

        Deserialize(json).LowStockVariants.Select(Describe).ShouldBe(ExpectedLowStock);
    }

    [Fact]
    public async Task LowerThreshold_ReturnsOnlyVariantsAtOrBelowIt()
    {
        var stats = await GetStatsAsync(SeedTime.AddDays(-30), SeedTime.AddMinutes(1), 1);

        stats.LowStockVariants.Select(Describe).ShouldBe(ExpectedLowStock.Take(2));
    }

    [Fact]
    public async Task OrderExactlyAtFrom_IsCounted()
    {
        // ORD-0001 was created exactly 28 days before the seed time.
        var stats = await GetStatsAsync(SeedTime.AddDays(-28), SeedTime.AddDays(-27), 3);

        stats.TotalOrders.ShouldBe(1);
        stats.TotalRevenue.ShouldBe(1850m);
    }

    [Fact]
    public async Task OrderExactlyAtTo_IsNotCounted()
    {
        var stats = await GetStatsAsync(SeedTime.AddDays(-29), SeedTime.AddDays(-28), 3);

        stats.TotalOrders.ShouldBe(0);
        stats.TotalRevenue.ShouldBe(0m);
    }

    [Fact]
    public async Task CancelledOrders_CountInTotalsButNotRevenueOrBestSellers()
    {
        // Only ORD-0003 (Cancelled, Floral Midi Dress) falls in this range.
        var stats = await GetStatsAsync(SeedTime.AddDays(-21), SeedTime.AddDays(-19), 3);

        stats.TotalOrders.ShouldBe(1);
        stats.OrdersByStatus.Cancelled.ShouldBe(1);
        stats.TotalRevenue.ShouldBe(0m);
        stats.BestSellers.ShouldBeEmpty();
    }

    private async Task<DashboardStats> GetStatsAsync(DateTime fromUtc, DateTime toUtc, int threshold) =>
        Deserialize(await GetJsonAsync(fromUtc, toUtc, threshold));

    private async Task<string> GetJsonAsync(DateTime fromUtc, DateTime toUtc, int threshold)
    {
        await using var context = _db.CreateContext();
        var rows = await context.Database
            .SqlQuery<string>($"EXEC dbo.sp_GetDashboardStats @FromUtc = {fromUtc}, @ToUtc = {toUtc}, @LowStockThreshold = {threshold}")
            .ToListAsync();
        return rows.ShouldHaveSingleItem();
    }

    private static DashboardStats Deserialize(string json) =>
        JsonSerializer.Deserialize<DashboardStats>(json, JsonOptions)!;

    private static string Describe(LowStockVariant v) =>
        $"{v.ProductName}/{v.ColorName}/{v.SizeName}/{v.Sku}/{v.Stock}";

    private sealed record DashboardStats(
        decimal TotalRevenue,
        int TotalOrders,
        OrdersByStatus OrdersByStatus,
        List<BestSeller> BestSellers,
        List<LowStockVariant> LowStockVariants);

    private sealed record OrdersByStatus(int Pending, int Confirmed, int Shipped, int Delivered, int Cancelled);

    private sealed record BestSeller(int ProductId, string ProductName, int UnitsSold);

    private sealed record LowStockVariant(int VariantId, string ProductName, string ColorName, string SizeName, string Sku, int Stock);
}

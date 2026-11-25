using Microsoft.EntityFrameworkCore;
using Shouldly;
using Store.Domain.Entities;
using Store.Domain.Enums;
using Store.Infrastructure.Tests.Fixtures;

namespace Store.Infrastructure.Tests;

/// <summary>User Story 1: applying migrations to an empty database creates the full schema.</summary>
public class SchemaTests : IAsyncLifetime
{
    private static readonly string[] ExpectedTables =
    [
        "Users", "Categories", "Products", "Colors", "Sizes",
        "ProductVariants", "ProductImages", "CartItems", "Orders", "OrderItems", "RefreshTokens"
    ];

    private static readonly string[] ExpectedCheckConstraints =
    [
        "CK_Products_Price", "CK_ProductVariants_Stock", "CK_CartItems_Quantity", "CK_OrderItems_Quantity"
    ];

    // data-model.md "Requirement mapping": FR-003 and FR-004.
    private static readonly string[] ExpectedUniqueIndexes =
    [
        "IX_Users_Email", "IX_Categories_Slug", "IX_Products_Slug", "IX_Colors_Name", "IX_Sizes_Name",
        "IX_ProductVariants_Sku", "IX_Orders_OrderNumber", "IX_ProductVariants_ProductId_ColorId_SizeId",
        "IX_CartItems_UserId_ProductVariantId", "IX_OrderItems_OrderId_ProductVariantId",
        "IX_RefreshTokens_TokenHash"
    ];

    // data-model.md "Requirement mapping": FR-015; 002 data-model.md "New: RefreshToken".
    private static readonly string[] ExpectedLookupIndexes =
    [
        "IX_Products_CategoryId", "IX_Products_IsActive_IsFeatured",
        "IX_Orders_UserId", "IX_Orders_Status", "IX_Orders_CreatedAt",
        "IX_RefreshTokens_UserId"
    ];

    private sealed record ColumnInfo(string Name, string Type, int? MaxLength, bool IsNullable);

    private readonly TestDatabase _db = new();

    public async Task InitializeAsync()
    {
        await using var context = _db.CreateContext();
        await context.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _db.DisposeAsync();

    [Fact]
    public async Task Migrate_OnEmptyDatabase_CreatesAllTables()
    {
        await using var context = _db.CreateContext();

        var tables = await context.Database
            .SqlQuery<string>($"SELECT TABLE_NAME AS [Value] FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE' AND TABLE_NAME <> '__EFMigrationsHistory'")
            .ToListAsync();

        tables.ShouldBe(ExpectedTables, ignoreOrder: true);
    }

    [Fact]
    public async Task Migrate_MapsEveryEntitySetToAnEmptyTable()
    {
        await using var context = _db.CreateContext();

        (await context.Users.CountAsync()).ShouldBe(0);
        (await context.Categories.CountAsync()).ShouldBe(0);
        (await context.Products.CountAsync()).ShouldBe(0);
        (await context.ProductVariants.CountAsync()).ShouldBe(0);
        (await context.ProductImages.CountAsync()).ShouldBe(0);
        (await context.CartItems.CountAsync()).ShouldBe(0);
        (await context.Orders.CountAsync()).ShouldBe(0);
        (await context.OrderItems.CountAsync()).ShouldBe(0);
        (await context.RefreshTokens.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Migrate_CreatesRefreshTokensColumns()
    {
        await using var context = _db.CreateContext();

        var columns = await context.Database
            .SqlQuery<ColumnInfo>($"""
                SELECT COLUMN_NAME AS Name, DATA_TYPE AS Type, CHARACTER_MAXIMUM_LENGTH AS MaxLength,
                       CAST(CASE WHEN IS_NULLABLE = 'YES' THEN 1 ELSE 0 END AS bit) AS IsNullable
                FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'RefreshTokens'
                """)
            .ToListAsync();

        columns.ShouldBe(
        [
            new ColumnInfo("Id", "int", null, false),
            new ColumnInfo("UserId", "int", null, false),
            new ColumnInfo("TokenHash", "varchar", 64, false),
            new ColumnInfo("CreatedAt", "datetime2", null, false),
            new ColumnInfo("ExpiresAt", "datetime2", null, false),
            new ColumnInfo("RevokedAt", "datetime2", null, true),
            new ColumnInfo("ReplacedByTokenHash", "varchar", 64, true)
        ], ignoreOrder: true);
    }

    [Fact]
    public async Task Migrate_RefreshTokensCascadeFromUsers()
    {
        await using var context = _db.CreateContext();

        var deleteAction = await context.Database
            .SqlQuery<string>($"""
                SELECT fk.delete_referential_action_desc AS [Value]
                FROM sys.foreign_keys fk
                WHERE fk.parent_object_id = OBJECT_ID('dbo.RefreshTokens')
                  AND fk.referenced_object_id = OBJECT_ID('dbo.Users')
                """)
            .ToListAsync();

        deleteAction.ShouldBe(["CASCADE"]);
    }

    [Fact]
    public async Task Migrate_CreatesAllCheckConstraints()
    {
        await using var context = _db.CreateContext();

        var constraints = await context.Database
            .SqlQuery<string>($"SELECT name AS [Value] FROM sys.check_constraints")
            .ToListAsync();

        constraints.ShouldBe(ExpectedCheckConstraints, ignoreOrder: true);
    }

    [Fact]
    public async Task Migrate_CreatesUniqueAndLookupIndexes()
    {
        await using var context = _db.CreateContext();

        var uniqueIndexes = await context.Database
            .SqlQuery<string>($"SELECT i.name AS [Value] FROM sys.indexes i JOIN sys.tables t ON t.object_id = i.object_id WHERE t.is_ms_shipped = 0 AND i.is_unique = 1 AND i.is_primary_key = 0")
            .ToListAsync();
        var nonUniqueIndexes = await context.Database
            .SqlQuery<string>($"SELECT i.name AS [Value] FROM sys.indexes i JOIN sys.tables t ON t.object_id = i.object_id WHERE t.is_ms_shipped = 0 AND i.is_unique = 0 AND i.name IS NOT NULL")
            .ToListAsync();

        uniqueIndexes.ShouldBe(ExpectedUniqueIndexes, ignoreOrder: true);
        foreach (var index in ExpectedLookupIndexes)
        {
            nonUniqueIndexes.ShouldContain(index);
        }
    }

    [Fact]
    public async Task Migrate_SeedsColorsAndSizes()
    {
        await using var context = _db.CreateContext();

        var colors = await context.Colors.OrderBy(c => c.Id).Select(c => c.Name).ToListAsync();
        var sizes = await context.Sizes.OrderBy(s => s.DisplayOrder).Select(s => s.Name).ToListAsync();

        colors.ShouldBe(["Black", "White", "Navy", "Gray", "Beige", "Red", "Blue", "Brown"]);
        sizes.ShouldBe(["XS", "S", "M", "L", "XL", "XXL", "38", "39", "40", "41", "42", "43", "44", "45"]);
    }

    [Fact]
    public async Task Migrate_AppliedTwice_LeavesNothingPending()
    {
        await using var context = _db.CreateContext();

        await context.Database.MigrateAsync();

        (await context.Database.GetPendingMigrationsAsync()).ShouldBeEmpty();
        (await context.Database.GetAppliedMigrationsAsync()).ShouldBe(context.Database.GetMigrations());
    }

    [Fact]
    public async Task DateTime_ReadBack_IsUtcWithSameValue()
    {
        var createdAt = new DateTime(2026, 11, 24, 10, 15, 30, DateTimeKind.Utc);
        await using (var context = _db.CreateContext())
        {
            context.Users.Add(new User("Test User", "test@store.local", "01000000000", "hash", Role.Customer, createdAt));
            await context.SaveChangesAsync();
        }

        await using var readContext = _db.CreateContext();
        var stored = await readContext.Users.SingleAsync();

        stored.CreatedAt.Kind.ShouldBe(DateTimeKind.Utc);
        stored.CreatedAt.ShouldBe(createdAt);
    }

    [Fact]
    public async Task RefreshToken_RoundTrip_KeepsUtcDatesAndDeletesWithUser()
    {
        var createdAt = new DateTime(2026, 11, 24, 12, 0, 0, DateTimeKind.Utc);
        await using (var context = _db.CreateContext())
        {
            var user = new User("Token User", "token@store.local", "01000000003", "hash", Role.Customer, createdAt);
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var token = new RefreshToken(user.Id, new string('A', 64), createdAt, createdAt.AddDays(7));
            token.MarkRotated(createdAt.AddMinutes(5), new string('B', 64));
            context.RefreshTokens.Add(token);
            await context.SaveChangesAsync();
        }

        await using (var readContext = _db.CreateContext())
        {
            var stored = await readContext.RefreshTokens.SingleAsync();
            stored.ExpiresAt.Kind.ShouldBe(DateTimeKind.Utc);
            stored.RevokedAt.ShouldBe(createdAt.AddMinutes(5));
            stored.RevokedAt.ShouldNotBeNull().Kind.ShouldBe(DateTimeKind.Utc);
            stored.IsRotated.ShouldBeTrue();

            readContext.Users.Remove(await readContext.Users.SingleAsync());
            await readContext.SaveChangesAsync();
        }

        await using var checkContext = _db.CreateContext();
        (await checkContext.RefreshTokens.CountAsync()).ShouldBe(0);
    }
}

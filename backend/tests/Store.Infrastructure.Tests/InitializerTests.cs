using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Shouldly;
using Store.Infrastructure.Persistence;
using Store.Infrastructure.Seeding;
using Store.Infrastructure.Tests.Fixtures;

namespace Store.Infrastructure.Tests;

/// <summary>Startup database work per environment (research R3, spec FR-018).</summary>
public class InitializerTests : IAsyncLifetime
{
    private readonly TestDatabase _db = new();
    private readonly ListLogger<DatabaseInitializer> _logger = new();

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => _db.DisposeAsync();

    [Fact]
    public async Task Development_MigratesAndSeeds()
    {
        await using var context = _db.CreateContext();

        await CreateInitializer(context).InitializeAsync(isDevelopment: true);

        (await context.Database.GetPendingMigrationsAsync()).ShouldBeEmpty();
        (await context.Users.CountAsync()).ShouldBe(2);
        (await context.Products.CountAsync()).ShouldBe(18);
        _logger.Entries.ShouldBeEmpty();
    }

    [Fact]
    public async Task NonDevelopment_SchemaCurrent_SeedsWithoutWarning()
    {
        await using var context = _db.CreateContext();
        await context.Database.MigrateAsync();

        await CreateInitializer(context).InitializeAsync(isDevelopment: false);

        (await context.Products.CountAsync()).ShouldBe(18);
        (await context.Orders.CountAsync()).ShouldBe(8);
        _logger.Entries.ShouldBeEmpty();
    }

    [Fact]
    public async Task NonDevelopment_MigrationsPending_WarnsWithCountAndSkipsSeeding()
    {
        await using var context = _db.CreateContext();
        await context.GetService<IRelationalDatabaseCreator>().CreateAsync();
        var migrationCount = context.Database.GetMigrations().Count();

        await CreateInitializer(context).InitializeAsync(isDevelopment: false);

        var entry = _logger.Entries.ShouldHaveSingleItem();
        entry.Level.ShouldBe(LogLevel.Warning);
        entry.Message.ShouldContain($"{migrationCount} pending migrations");
        (await context.Database.GetAppliedMigrationsAsync()).ShouldBeEmpty();
        var tables = await context.Database
            .SqlQuery<string>($"SELECT TABLE_NAME AS [Value] FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'")
            .ToListAsync();
        tables.ShouldBeEmpty();
    }

    private DatabaseInitializer CreateInitializer(AppDbContext context) =>
        new(context, Seeders.CreateDatabaseSeeder(context, Seeders.DefaultOptions()), _logger);
}

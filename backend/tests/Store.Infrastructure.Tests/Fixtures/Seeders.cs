using Microsoft.Extensions.Options;
using Store.Infrastructure.Persistence;
using Store.Infrastructure.Seeding;

namespace Store.Infrastructure.Tests.Fixtures;

/// <summary>Builds the seeders the way DI does, with a fixed seed time.</summary>
public static class Seeders
{
    public static readonly DateTimeOffset SeedTime = new(2026, 11, 24, 12, 0, 0, TimeSpan.Zero);

    public const string AdminPassword = "Admin#Test-Only-1";
    public const string CustomerPassword = "Customer#Test-Only-1";

    public static SeedOptions DefaultOptions() => new()
    {
        AdminPassword = AdminPassword,
        CustomerPassword = CustomerPassword
    };

    public static DatabaseSeeder CreateDatabaseSeeder(
        AppDbContext context,
        SeedOptions options,
        ListLogger<CatalogSeeder>? catalogLogger = null)
    {
        var wrapped = Options.Create(options);
        var time = new FixedTimeProvider(SeedTime);
        return new DatabaseSeeder(
            wrapped,
            new AccountSeeder(context, wrapped, time),
            CreateCatalogSeeder(context, options, catalogLogger));
    }

    public static CatalogSeeder CreateCatalogSeeder(
        AppDbContext context,
        SeedOptions options,
        ListLogger<CatalogSeeder>? logger = null) =>
        new(context, Options.Create(options), new FixedTimeProvider(SeedTime), logger ?? new ListLogger<CatalogSeeder>());
}

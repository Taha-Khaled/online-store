using Microsoft.Extensions.Options;

namespace Store.Infrastructure.Seeding;

/// <summary>Runs the seeding groups in order: accounts first, then catalog and demo orders.</summary>
public sealed class DatabaseSeeder(IOptions<SeedOptions> options, AccountSeeder accountSeeder, CatalogSeeder catalogSeeder)
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (!options.Value.Enabled)
        {
            return;
        }

        await accountSeeder.SeedAsync(cancellationToken);
        await catalogSeeder.SeedAsync(cancellationToken);
    }
}

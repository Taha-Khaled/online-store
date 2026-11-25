using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Store.Application.Common;

namespace Store.Infrastructure.Seeding;

/// <summary>
/// Startup database work (research R3): Development migrates then seeds; other environments never
/// migrate and only seed when the schema is current, otherwise they warn and start anyway.
/// </summary>
public sealed class DatabaseInitializer(IAppDbContext db, DatabaseSeeder seeder, ILogger<DatabaseInitializer> logger)
{
    public async Task InitializeAsync(bool isDevelopment, CancellationToken cancellationToken = default)
    {
        if (isDevelopment)
        {
            await db.Database.MigrateAsync(cancellationToken);
            await seeder.SeedAsync(cancellationToken);
            return;
        }

        var pendingCount = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).Count();
        if (pendingCount > 0)
        {
            logger.LogWarning(
                "{PendingCount} pending migrations; seeding skipped until the schema is applied",
                pendingCount);
            return;
        }

        await seeder.SeedAsync(cancellationToken);
    }
}

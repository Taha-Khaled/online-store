using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Store.Application.Common;
using Store.Application.Common.Security;
using Store.Domain.Entities;
using Store.Domain.Enums;
using Store.Infrastructure.Seeding.Data;

namespace Store.Infrastructure.Seeding;

/// <summary>Creates each seed account whose email does not exist yet (spec FR-019, FR-020).</summary>
public sealed class AccountSeeder(
    IAppDbContext db,
    IOptions<SeedOptions> options,
    IPasswordHasher passwordHasher,
    TimeProvider timeProvider)
{
    private const string AdminPasswordKey = "Seed:AdminPassword";
    private const string CustomerPasswordKey = "Seed:CustomerPassword";

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var createdAt = timeProvider.GetUtcNow().UtcDateTime;

        foreach (var account in AccountSeedData.Accounts)
        {
            if (await db.Users.AnyAsync(u => u.Email == account.Email, cancellationToken))
            {
                continue;
            }

            var password = GetRequiredPassword(account.Role);
            var passwordHash = passwordHasher.Hash(password);
            db.Users.Add(new User(account.FullName, account.Email, account.Phone, passwordHash, account.Role, createdAt));
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private string GetRequiredPassword(Role role)
    {
        var (password, key) = role == Role.Admin
            ? (options.Value.AdminPassword, AdminPasswordKey)
            : (options.Value.CustomerPassword, CustomerPasswordKey);

        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException($"Seed password '{key}' is not configured.");
        }

        return password;
    }
}

using Store.Domain.Enums;

namespace Store.Infrastructure.Seeding.Data;

public sealed record AccountSeed(string Email, string FullName, string Phone, Role Role);

/// <summary>Seed accounts (spec.md "Seed Data → Accounts"). Passwords come from configuration.</summary>
public static class AccountSeedData
{
    public const string AdminEmail = "admin@store.local";
    public const string DemoCustomerEmail = "customer@store.local";

    public static readonly IReadOnlyList<AccountSeed> Accounts =
    [
        new(AdminEmail, "Store Admin", "01000000001", Role.Admin),
        new(DemoCustomerEmail, "Demo Customer", "01000000002", Role.Customer)
    ];
}

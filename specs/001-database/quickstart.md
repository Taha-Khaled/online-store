# Quickstart: Store Database & Seed Data (001-database)

## Prerequisites

- .NET 10 SDK (10.0.3xx)
- Local SQL Server (named instance `localhost\MSSQLSERVER01`), Windows authentication
- EF Core tools: `dotnet tool restore` from `backend/` (local tool manifest added by T002)

## Configuration (never committed)

User Secrets are initialized for Store.API by task T010 (`dotnet user-secrets init`). Set these
three secrets from `backend/src/Store.API`; all are required when seeding is enabled:

| Secret | Purpose |
|---|---|
| `ConnectionStrings:Store` | local SQL Server database for the API |
| `Seed:AdminPassword` | password of `admin@store.local` |
| `Seed:CustomerPassword` | password of `customer@store.local` |

```powershell
dotnet user-secrets set "ConnectionStrings:Store" "Server=localhost\MSSQLSERVER01;Database=OnlineStore;Integrated Security=true;TrustServerCertificate=true"
dotnet user-secrets set "Seed:AdminPassword" "<choose one>"
dotnet user-secrets set "Seed:CustomerPassword" "<choose one>"
```

For integration tests, set a server-only connection string (no database name):

```powershell
$env:STORE_TEST_SQLSERVER = "Server=localhost\MSSQLSERVER01;Integrated Security=true;TrustServerCertificate=true"
```

## Validate

1. **Schema + seed (User Stories 1–2)**: run the API in Development
   (`dotnet run --project backend/src/Store.API`). Startup applies migrations and seeds.
   Expected counts (SC-002): 2 users, 8 colors, 14 sizes, 12 categories, 18 products,
   177 variants, 37 images, 8 orders, 12 order items. Restart: counts unchanged.
2. **Missing password**: remove `Seed:AdminPassword`, start against a new database: startup
   fails with a message naming `Seed:AdminPassword`; no user rows exist.
3. **Seeding off**: set `Seed:Enabled` to `false`, start against a new database: startup
   succeeds; only colors and sizes exist.
4. **Statistics (User Story 5)**: compare with the expected values in
   [contracts/sp_GetDashboardStats.md](contracts/sp_GetDashboardStats.md).
5. **Full suite** (User Stories 1–5, SC-002 to SC-009):

   ```powershell
   dotnet test backend --collect:"XPlat Code Coverage"
   ```

   Every test creates and drops its own `StoreTests_<guid>` database.

## Production notes

- Startup never migrates in Production. Apply migrations to the hosted database only with the
  developer's approval (`dotnet ef database update` or a generated script).
- After the schema is current, the next startup seeds (passwords from `Seed__AdminPassword` and
  `Seed__CustomerPassword` environment variables).

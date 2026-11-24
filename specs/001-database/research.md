# Research: Store Database & Seed Data (001-database)

Most decisions were supplied by the developer. This file records the decisions that needed
checking, and the gaps found while turning them into a design.

## R1. Integration test database

- **Decision**: Use the local SQL Server instance (named instance `localhost\MSSQLSERVER01`), not
  Testcontainers. Each test creates a uniquely named database (`StoreTests_<guid>`), applies
  migrations, and drops it in `DisposeAsync` (`EnsureDeletedAsync`). The server connection
  string comes from the environment variable `STORE_TEST_SQLSERVER`; if it is missing, the
  fixture fails with a message naming the variable.
- **Rationale**: Docker is not installed on the development machine; the local instance is
  running, and the constitution allows the agent to use the local SQL Server freely.
- **Alternatives considered**: Testcontainers.MsSql (needs Docker; Docker Desktop needs a paid
  licence for large companies). SQLite/in-memory providers (cannot test rowversion, check
  constraints, or the stored procedure). Rejected.
- **Consequence**: `Testcontainers.MsSql` is not added. A future CI pipeline needs a SQL Server
  service.
- **Status**: approved by the developer (constitution 1.7.0).

## R2. Reading the stored procedure's JSON

- **Decision**: The procedure builds the JSON into an `nvarchar(max)` variable and returns it
  with `SELECT @json AS [Value]`. C# calls
  `Database.SqlQuery<string>($"EXEC dbo.sp_GetDashboardStats @FromUtc = {from}, @ToUtc = {to}, @LowStockThreshold = {threshold}")`
  and materializes with `ToListAsync()`, then takes the single row.
- **Rationale**:
  - `SELECT ... FOR JSON` returned directly is split by SQL Server into multiple rows of about
    2,033 characters when large. Assigning to a variable first always gives one row.
  - EF Core maps a scalar `SqlQuery<T>` to a column named `Value`.
  - An `EXEC` cannot be composed into a subquery, so LINQ operators such as `FirstAsync()`
    must not be applied before materializing; `ToListAsync()` sends the SQL unchanged.
- **Alternatives considered**: returning the JSON directly (row-splitting bug); `FromSqlRaw`
  (forbidden by the constitution).

## R3. Where and when seeding runs

- **Decision** (developer decision, constitution 1.7.0):
  - EF Core `UseSeeding` / `UseAsyncSeeding` are **not** used.
  - One `DatabaseSeeder` class (Store.Infrastructure/Seeding) runs both groups. `Program.cs`
    calls `DatabaseSeeder.SeedAsync` explicitly at startup in every environment.
  - Development: `MigrateAsync()` first, then the seeder.
  - Other environments: no migration. The seeder runs only if `GetPendingMigrationsAsync()` is
    empty. Otherwise the application **still starts**: it logs one warning naming the number of
    pending migrations (e.g. "2 pending migrations; seeding skipped") and skips seeding, because
    the tables may not exist yet (migrations are applied separately, with approval).
  - This startup logic lives in `DatabaseInitializer.InitializeAsync(isDevelopment)` in
    Store.Infrastructure, so it is testable; `Program.cs` only calls it.
  - **Test**: create an empty database with no schema (`IRelationalDatabaseCreator.CreateAsync`),
    call `InitializeAsync(isDevelopment: false)`, and assert: no exception; exactly one warning
    containing the pending-migration count (2), captured by a small in-test `ILogger`
    implementation; no migrations applied; no seed rows.
  - `Seed:Enabled` (default `true`): when `false`, `SeedAsync` returns immediately, before reading
    any other `Seed:*` setting or touching the database.
  - `dotnet ef database update` only applies the schema; the next application start seeds.
- **Rationale**: one explicit call path is easier to follow and test than an EF hook that only
  fires from `Migrate`/`EnsureCreated`, which Production never calls. The switch lets an
  environment run without demo data.
- **Alternatives considered**: `UseAsyncSeeding` in Development plus a direct call in
  Production (two paths, rejected by the developer); calling `MigrateAsync()` in Production
  (applies migrations without approval, violates the Deployment rule).

## R4. Transaction for the catalog group

- **Decision**: The catalog group opens its own transaction (`BeginTransactionAsync`), saves all
  rows, and commits at the end; any exception disposes the transaction without committing.
  Connection retry (`EnableRetryOnFailure`) is **not** enabled, so the user-opened transaction
  needs no execution-strategy wrapper.
- **Rationale**: the seeder is no longer called from inside `Migrate` (R3), so there is never an
  outer transaction to reuse.

## R5. Injecting a failure for SC-008 without test-only code

- **Decision**: The rollback test uses a real conflict: it inserts one category whose slug
  matches a seed category (no products), runs the seeder, and asserts it throws and that no
  products, variants, images or orders exist and the only category is the pre-inserted one. A
  second test removes the demo customer before the catalog group runs and asserts the same.
- **Rationale**: these are the two failure edge cases in the spec, so no production test hook
  is needed.

## R6. Time source

- **Decision**: The seeder takes `TimeProvider` (built into .NET; `TimeProvider.System` is
  registered in DI). Tests pass a small `TimeProvider` subclass with a fixed `GetUtcNow()`.
- **Rationale**: the dashboard range ends exclusively at "now". If a test's "now" equals the
  seed time (coarse clocks), ORD-0008 would be excluded. A fixed seed time lets tests query
  `[seedTime - 30 days, seedTime + 1 minute)` deterministically. No extra package is needed.

## R7. Testing database rules without EF Core's client-side checks

- **Decision**: Delete-behavior tests use `ExecuteDeleteAsync()` and constraint tests use
  `ExecuteUpdateAsync()` or a fresh context, so the statement reaches SQL Server.
- **Rationale**: with tracked entities, EF Core may cascade or throw on the client before SQL
  Server sees the statement, which would hide a missing database rule.

## R8. Cascade paths on SQL Server

- **Decision**: The delete behaviors as given are valid. `CartItems` is reached by cascade from
  `Users` and from `Products → ProductVariants`, which start at different tables, so SQL Server
  does not report multiple cascade paths. `Restrict` is generated as `NO ACTION`.
- **Check**: verified by the migration-applies test (User Story 1).

## R9. Deterministic stock rule

- **Decision**: `stock = 8 + ((productNumber * 31 + colorIndex * 7 + sizeDisplayOrder * 3) % 13)`
  for every variant not in the special-stock table. `productNumber` is 1–18, `colorIndex` is the
  0-based position of the color in that product's color list, `sizeDisplayOrder` is the size's
  display order. The result is always 8–20.
- **Example**: Classic Cotton T-Shirt (1), White (index 1), M (3):
  `8 + ((31 + 7 + 9) % 13) = 8 + 8 = 16`.
- **Rationale**: no randomness, and the lowest value (8) is above the low-stock threshold, so
  only the four special variants are ever low stock.

## R10. Packages and framework references

- **Decision**:
  - `PasswordHasher<User>` comes from the ASP.NET Core shared framework. Store.Infrastructure
    adds `<FrameworkReference Include="Microsoft.AspNetCore.App" />`; no NuGet package.
  - `IAppDbContext` in Store.Application exposes `DbSet<T>` and `DatabaseFacade`, so
    Store.Application needs the base `Microsoft.EntityFrameworkCore` package. It is not named in
    the constitution's approved list at 1.5.0; **approved** in constitution 1.7.0.
  - Test packages: `xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`, `Shouldly`,
    `coverlet.collector` (all approved). `Testcontainers.MsSql` is dropped (R1).

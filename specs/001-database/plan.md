# Implementation Plan: Store Database & Seed Data

**Branch**: `feat/001-database` | **Date**: 2026-11-24 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/001-database/spec.md`

## Summary

Create the backend solution skeleton and the complete SQL Server database for the store: 10
entities with Fluent API configurations, check constraints, unique indexes, delete behaviors and
a rowversion concurrency token; two migrations (`InitialCreate` with colors/sizes via `HasData`,
and `AddDashboardStatsProcedure`); idempotent runtime seeding in two groups (accounts per email;
catalog + demo orders all-or-nothing when no products exist) with deterministic stock; and
integration tests against the local SQL Server proving every acceptance scenario. No controllers,
services or auth logic.

## Technical Context

**Language/Version**: C# 14 on .NET 10 (SDK 10.0.301 installed)

**Primary Dependencies**: EF Core 10 (`Microsoft.EntityFrameworkCore.SqlServer`, `.Design`,
`.Tools`); base `Microsoft.EntityFrameworkCore` in Store.Application (approved, constitution
1.7.0); ASP.NET Core shared framework for `PasswordHasher<User>`

**Storage**: SQL Server (local named instance `localhost\MSSQLSERVER01`; MonsterASP.NET in
production)

**Testing**: xUnit, Shouldly, coverlet; integration tests on the local SQL Server, one fresh
database per test ([research R1](research.md#r1-integration-test-database))

**Target Platform**: ASP.NET Core Web API on Windows/IIS (MonsterASP.NET)

**Project Type**: web service (backend only for this feature)

**Performance Goals**: not critical at this scale; dashboard statistics in one round trip

**Constraints**: no raw SQL APIs; no secrets in code; seeding idempotent and transactional;
stock deterministic

**Scale/Scope**: 10 tables, 1 stored procedure, 177 variants, 8 orders of seed data

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle / rule | Status | How |
|---|---|---|
| I. Four projects; Domain has no dependencies | ✅ | Store.Domain references nothing |
| I. Rules in Domain/Application, DB constraints as safety net | ✅ | constraints here duplicate rules; app-side rules come with later services |
| I. `Order` owns status transitions | ⏳ deferred | entities are data-only here; transitions are a later feature (agreed) |
| II. EF Core Code First, no generic repository, `IAppDbContext` | ✅ | `AppDbContext : IAppDbContext` |
| II. Fluent API in separate classes, no annotations | ✅ | one `IEntityTypeConfiguration<T>` per entity |
| II. Named migrations only | ✅ | `InitialCreate`, `AddDashboardStatsProcedure` |
| II. One stored procedure via `migrationBuilder.Sql`, `FOR JSON PATH` | ✅ | [contract](contracts/sp_GetDashboardStats.md) |
| III. No `FromSqlRaw`/`ExecuteSqlRaw`; `SqlQuery` interpolated | ✅ | [research R2](research.md#r2-reading-the-stored-procedures-json) |
| III. Procedure has no dynamic SQL | ✅ | static SQL with parameters |
| III. `PasswordHasher`, secrets from config | ✅ | `Seed:*` keys from User Secrets / env vars |
| IV. Stock per variant, rowversion, `Stock >= 0` | ✅ | data-model |
| V. `decimal(18,2)`, UTC, snapshots, deactivate not delete | ✅ | data-model; OrderItem → Variant is Restrict |
| Seed Data: `HasData` for lookups, explicit `DatabaseSeeder` at startup, no `UseSeeding` | ✅ | [research R3](research.md#r3-where-and-when-seeding-runs) |
| Seed Data: `Seed:Enabled` switch (default true) | ✅ | checked first in `SeedAsync` |
| Deployment: hosted migrations need approval | ✅ | only Development startup migrates |
| Workflow: approved dependencies only | ✅ | all packages on the 1.7.0 list; integration tests on local SQL Server |
| Workflow: 80% coverage with exclusions | ✅ | seeder logic covered by integration tests |

**Result**: PASS (constitution 1.7.0). Re-checked after Phase 1: no change.

## Project Structure

### Documentation (this feature)

```text
specs/001-database/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── sp_GetDashboardStats.md
├── checklists/requirements.md
└── tasks.md              # created by /speckit-tasks
```

### Source Code (repository root)

```text
backend/
├── Store.slnx
├── src/
│   ├── Store.Domain/
│   │   ├── Entities/          # User, Category, Product, Color, Size, ProductVariant,
│   │   │                      # ProductImage, CartItem, Order, OrderItem
│   │   └── Enums/             # Role, OrderStatus
│   ├── Store.Application/
│   │   └── Common/IAppDbContext.cs
│   ├── Store.Infrastructure/
│   │   ├── Persistence/
│   │   │   ├── AppDbContext.cs
│   │   │   ├── Configurations/    # one class per entity (+ HasData for Color, Size)
│   │   │   └── Migrations/        # InitialCreate, AddDashboardStatsProcedure
│   │   ├── Seeding/
│   │   │   ├── DatabaseInitializer.cs # Dev: migrate+seed; else: seed or warn if pending
│   │   │   ├── DatabaseSeeder.cs      # runs groups in order
│   │   │   ├── AccountSeeder.cs
│   │   │   ├── CatalogSeeder.cs       # transaction, stock rule, SKUs, images, orders
│   │   │   ├── SeedOptions.cs         # Enabled, passwords, images path
│   │   │   └── Data/                  # static seed content (excluded from coverage)
│   │   └── DependencyInjection.cs
│   └── Store.API/
│       ├── Program.cs             # DI; calls DatabaseInitializer.InitializeAsync
│       └── SeedImages/            # provided .jpg files, copied to output
└── tests/
    ├── Store.Domain.Tests/         # project skeleton only; tests arrive with Order logic
    └── Store.Infrastructure.Tests/
        ├── Fixtures/               # per-test database, fixed TimeProvider
        ├── SchemaTests.cs          # US1
        ├── SeedingTests.cs         # US2, SC-002, SC-007, SC-008, SC-009
        ├── InitializerTests.cs     # pending migrations outside Development: warn + skip
        ├── ConstraintTests.cs      # US3
        ├── ConcurrencyTests.cs     # US4
        └── DashboardStatsTests.cs  # US5
```

**Structure Decision**: backend-only web service following the constitution's four-project
layout under `/backend`. `Store.Domain.Tests` is created empty to match the agreed structure; no
placeholder tests are written.

## Key design points

- **Seeding flow** ([research R3–R6](research.md)): `Program.cs` calls
  `DatabaseInitializer.InitializeAsync`, which runs `DatabaseSeeder.SeedAsync` after
  `MigrateAsync` in Development; elsewhere it seeds only when no migrations are pending, and
  otherwise the app still starts, logs a warning with the pending count, and skips seeding. `SeedAsync` returns at once if `Seed:Enabled` is false; otherwise → `AccountSeeder`
  (per email; throws naming the missing `Seed:*` key) → `CatalogSeeder` (skips if any product;
  otherwise one own transaction; throws if the demo customer is missing). No `UseSeeding`.
- **Seed:Enabled test (SC-009)**: run the seeder on a fresh database with `Enabled = false` and
  no passwords configured; assert it completes and every runtime-seeded table is empty.
- **Stock**: special values from the spec table; all others use the rule in
  [research R9](research.md#r9-deterministic-stock-rule). Stored as final values; seeding runs no
  deduction logic.
- **UTC on read**: `AppDbContext.ConfigureConventions` applies a `DateTime` value converter that
  marks every value read from the database as `DateTimeKind.Utc` (constitution V), so later API
  responses serialize dates with a UTC marker.
- **Data access**: seeders and `DatabaseInitializer` depend on `IAppDbContext`, never on
  `AppDbContext` directly (constitution II).
- **Historical orders**: `Order` has a private constructor and one static factory for historical
  / seed orders that accepts any status; status transitions arrive in a later feature.
- **Dates**: seed time `T` from `TimeProvider`; orders `T − N days` (created = updated); products
  `T − 60 days`.
- **Images**: URL `/images/products/<product-slug>-<color>.jpg`; a missing file under
  `SeedOptions.ImagesPath` logs a warning with the file name only; the row is still inserted.
- **Coverage exclusions**: `Persistence/Migrations`, `Persistence/Configurations`, `Seeding/Data`,
  `Program.cs`, `DependencyInjection.cs` (constitution Project Workflow).

## Complexity Tracking

No constitution violations requiring justification.

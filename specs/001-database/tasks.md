---

description: "Task list for 001-database: Store Database & Seed Data"
---

# Tasks: Store Database & Seed Data

**Input**: Design documents from `/specs/001-database/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md),
[data-model.md](data-model.md), [contracts/sp_GetDashboardStats.md](contracts/sp_GetDashboardStats.md),
[quickstart.md](quickstart.md)

**Tests**: Required (plan.md, constitution Quality Gates, 80% coverage rule). Integration tests run
against the local SQL Server, one fresh database per test (research R1). Within each story the
implementation task comes first and its test task follows, so every commit builds.

**Work items**: each task ID is the work item number in branch/commit/PR names, written
`#001-<task>` (e.g. `feat #001-T006 add catalog entities`). Exactly one task per commit; never
group several tasks in one commit. A task that ends with no code change (e.g. T030) produces no
commit.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: User story from spec.md (US1–US5)
- Paths are relative to the repository root; backend code lives under `backend/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Solution skeleton, packages, build and coverage settings

- [X] T001 Create `backend/Store.slnx` with projects `backend/src/Store.Domain`, `backend/src/Store.Application`, `backend/src/Store.Infrastructure` (class libraries), `backend/src/Store.API` (ASP.NET Core Web API, no controllers yet), `backend/tests/Store.Domain.Tests` and `backend/tests/Store.Infrastructure.Tests` (xUnit), all `net10.0`; project references Application→Domain, Infrastructure→Application+Domain, API→Infrastructure+Application, Infrastructure.Tests→Infrastructure, Domain.Tests→Domain; add `backend/Directory.Build.props` (Nullable enable, ImplicitUsings enable, TreatWarningsAsErrors true, LangVersion 14) and a `backend/.gitignore` for bin/obj if the repository has none; remove template sample files (e.g. WeatherForecast, Class1.cs, UnitTest1.cs)
- [X] T002 Add packages (all approved, constitution 1.7.1): `Microsoft.EntityFrameworkCore` in `backend/src/Store.Application/Store.Application.csproj`; `Microsoft.EntityFrameworkCore.SqlServer` and `<FrameworkReference Include="Microsoft.AspNetCore.App" />` in `backend/src/Store.Infrastructure/Store.Infrastructure.csproj` (research R10); `Microsoft.EntityFrameworkCore.Design` in `backend/src/Store.API/Store.API.csproj`; `xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`, `Shouldly`, `coverlet.collector` in both test projects; add a local tool manifest `backend/dotnet-tools.json` (SDK 10 default location) with `dotnet-ef`; pin EF Core packages and `dotnet-ef` to 10.0.x (11.x targets net11.0)
- [X] T003 [P] Create `backend/coverage.runsettings` excluding from coverage: `**/Persistence/Migrations/**`, `**/Persistence/Configurations/**`, `**/Seeding/Data/**`, `**/Program.cs`, `**/DependencyInjection.cs` (plan.md "Coverage exclusions")

**Checkpoint**: `dotnet build backend` succeeds with zero warnings

**Developer step (not a task)**: after T001, the developer places the 37 existing seed images in
`backend/src/Store.API/SeedImages/`. They stay uncommitted until T026, which commits them with the
copy-to-output rule.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Entities, DbContext, DI and the test fixture that every story uses

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T004 [P] Create enums `Role` and `OrderStatus` in `backend/src/Store.Domain/Enums/Role.cs` and `backend/src/Store.Domain/Enums/OrderStatus.cs` with the values in data-model.md "Enums"
- [X] T005 [P] Create entities `User`, `Category`, `Color`, `Size` in `backend/src/Store.Domain/Entities/` with private setters and constructors taking required values (data-only, no logic): fields per data-model.md User.Id, User.FullName, User.Email, User.Phone, User.PasswordHash, User.Role, User.CreatedAt; Category.Id, Category.Name, Category.Slug, Category.ParentCategoryId (nullable, navigation to parent and children); Color.Id, Color.Name, Color.HexCode; Size.Id, Size.Name, Size.DisplayOrder
- [X] T006 [P] Create entities `Product`, `ProductVariant`, `ProductImage` in `backend/src/Store.Domain/Entities/` (same style as T005): data-model.md Product.Id, Product.CategoryId, Product.Name, Product.Slug, Product.Description, Product.Price, Product.IsActive, Product.IsFeatured, Product.CreatedAt; ProductVariant.Id, ProductVariant.ProductId, ProductVariant.ColorId, ProductVariant.SizeId, ProductVariant.Sku, ProductVariant.Stock, ProductVariant.RowVersion (`byte[]`); ProductImage.Id, ProductImage.ProductId, ProductImage.ColorId (nullable), ProductImage.Url, ProductImage.DisplayOrder, ProductImage.IsMain; navigations Product→Variants/Images
- [X] T007 [P] Create entities `CartItem`, `Order`, `OrderItem` in `backend/src/Store.Domain/Entities/` (same style as T005; no status-transition logic): data-model.md CartItem.Id, CartItem.UserId, CartItem.ProductVariantId, CartItem.Quantity, CartItem.AddedAt; Order.Id, Order.OrderNumber, Order.UserId, Order.Status, Order.Subtotal, Order.ShippingFee, Order.Total, Order.ShippingName, Order.ShippingPhone, Order.City, Order.AddressLine, Order.Notes (nullable), Order.CreatedAt, Order.UpdatedAt; OrderItem.Id, OrderItem.OrderId, OrderItem.ProductVariantId, OrderItem.ProductName, OrderItem.ColorName, OrderItem.SizeName, OrderItem.UnitPrice, OrderItem.Quantity; navigation Order→Items. `Order` has a private constructor and exactly one static factory `Order.CreateHistorical(...)` for historical/seed orders that takes all fields above, any status and the items, and sets `UpdatedAt = CreatedAt`; no other way to create an `Order` in this feature, no extra abstractions
- [X] T008 Create `IAppDbContext` in `backend/src/Store.Application/Common/IAppDbContext.cs` exposing a `DbSet<T>` for each of the 10 entities, `DatabaseFacade Database` and `SaveChangesAsync(CancellationToken)` (depends on T005–T007)
- [X] T009 Create `AppDbContext : DbContext, IAppDbContext` in `backend/src/Store.Infrastructure/Persistence/AppDbContext.cs`, applying configurations with `ApplyConfigurationsFromAssembly`; in `ConfigureConventions`, apply a `DateTime` value converter that stores values unchanged and marks values read from the database as `DateTimeKind.Utc` (data-model.md User.CreatedAt, Product.CreatedAt, CartItem.AddedAt, Order.CreatedAt, Order.UpdatedAt; constitution V); no data annotations anywhere (constitution II)
- [X] T010 Create `AddInfrastructure(IServiceCollection, IConfiguration)` in `backend/src/Store.Infrastructure/DependencyInjection.cs`: `AddDbContext<AppDbContext>` with `UseSqlServer(ConnectionStrings:Store)` (no `EnableRetryOnFailure`, research R4), `IAppDbContext` → `AppDbContext`, `TimeProvider.System` singleton; wire it in `backend/src/Store.API/Program.cs` (no controllers, no seeding yet); run `dotnet user-secrets init` for `backend/src/Store.API/Store.API.csproj` (adds `UserSecretsId`; secret values are set by the developer per [quickstart.md](quickstart.md) "Configuration", never committed); `backend/src/Store.API/appsettings.json` contains no connection string or secrets
- [X] T011 Create the test fixtures in `backend/tests/Store.Infrastructure.Tests/Fixtures/`: `TestDatabase.cs` (reads `STORE_TEST_SQLSERVER`, fails with a message naming it when missing; builds a `StoreTests_<guid>` database name; creates `AppDbContext` instances; `IAsyncLifetime` that drops the database with `EnsureDeletedAsync`), `FixedTimeProvider.cs` (`TimeProvider` subclass with fixed `GetUtcNow()`, research R6), `ListLogger.cs` (minimal `ILogger<T>` that records level + message)

**Checkpoint**: solution builds; fixture can create and drop an empty database

---

## Phase 3: User Story 1 - Store structure is created from nothing (Priority: P1) 🎯 MVP

**Goal**: Applying migrations to an empty database creates every table, constraint and index

**Independent Test**: apply migrations to a fresh database; inspect tables, check constraints and
indexes; apply again and see nothing pending

### Implementation for User Story 1

- [X] T012 [P] [US1] Create `UserConfiguration` and `CategoryConfiguration` in `backend/src/Store.Infrastructure/Persistence/Configurations/`: lengths, uniqueness, enum-as-string and delete behaviors per data-model.md User.FullName, User.Email, User.Phone, User.PasswordHash, User.Role, User.CreatedAt; Category.Name, Category.Slug, Category.ParentCategoryId; relationships User→Orders Restrict, User→CartItems Cascade, Category→Products Restrict, Category→children NoAction
- [X] T013 [P] [US1] Create `ColorConfiguration` and `SizeConfiguration` in `backend/src/Store.Infrastructure/Persistence/Configurations/` per data-model.md Color.Name, Color.HexCode, Size.Name, Size.DisplayOrder, with `HasData` using fixed Ids (colors 1–8, sizes 1–14) and the values in spec.md "Seed Data" (Colors, Sizes)
- [X] T014 [P] [US1] Create `ProductConfiguration`, `ProductVariantConfiguration`, `ProductImageConfiguration` in `backend/src/Store.Infrastructure/Persistence/Configurations/` per data-model.md Product.Name, Product.Slug, Product.Description, Product.Price (`HasPrecision(18,2)`, check `CK_Products_Price`), Product.CategoryId, Product.IsActive, Product.IsFeatured, Product.CreatedAt (`datetime2`); ProductVariant.Sku, ProductVariant.Stock (check `CK_ProductVariants_Stock`), ProductVariant.RowVersion (`IsRowVersion()`), unique `(ProductId, ColorId, SizeId)`; ProductImage.Url, ProductImage.ColorId, ProductImage.DisplayOrder, ProductImage.IsMain; indexes `IX_Products_CategoryId`, `IX_Products_IsActive_IsFeatured`; delete behaviors Product→Variants/Images Cascade, Variant→CartItems Cascade, Variant→OrderItems Restrict, Color/Size→Variants/Images Restrict
- [X] T015 [P] [US1] Create `CartItemConfiguration`, `OrderConfiguration`, `OrderItemConfiguration` in `backend/src/Store.Infrastructure/Persistence/Configurations/` per data-model.md CartItem.Quantity (check `CK_CartItems_Quantity`), CartItem.AddedAt, unique `(UserId, ProductVariantId)`; Order.OrderNumber (unique), Order.Status, Order.Subtotal, Order.ShippingFee, Order.Total, Order.ShippingName, Order.ShippingPhone, Order.City, Order.AddressLine, Order.Notes, Order.CreatedAt, Order.UpdatedAt; indexes `IX_Orders_UserId`, `IX_Orders_Status`, `IX_Orders_CreatedAt`; OrderItem.ProductName, OrderItem.ColorName, OrderItem.SizeName, OrderItem.UnitPrice, OrderItem.Quantity (check `CK_OrderItems_Quantity`), unique `(OrderId, ProductVariantId)`; Order→OrderItems Cascade
- [X] T016 [US1] Generate migration `InitialCreate` into `backend/src/Store.Infrastructure/Persistence/Migrations/` (`dotnet ef migrations add InitialCreate --project src/Store.Infrastructure --startup-project src/Store.API --output-dir Persistence/Migrations`); review the generated code against data-model.md (no multiple-cascade-path errors, research R8); apply it to the local SQL Server

### Tests for User Story 1

- [X] T017 [US1] Write `backend/tests/Store.Infrastructure.Tests/SchemaTests.cs`: (a) `MigrateAsync` on an empty database succeeds and all 10 tables exist; (b) the four check constraints `CK_Products_Price`, `CK_ProductVariants_Stock`, `CK_CartItems_Quantity`, `CK_OrderItems_Quantity` exist (query `sys.check_constraints` with `SqlQuery<string>`); (c) unique and lookup indexes from data-model.md "Requirement mapping" exist (`sys.indexes`); (d) colors (8) and sizes (14) are present; (e) a second `MigrateAsync` leaves `GetPendingMigrationsAsync()` empty; (f) a saved `User.CreatedAt` read back in a fresh context has `Kind == DateTimeKind.Utc` and the same value (T009 converter); all green

**Checkpoint**: US1 tests pass; the schema exists with all safety-net rules

---

## Phase 4: User Story 2 - The store never opens empty (Priority: P1)

**Goal**: Startup seeds accounts and the catalog + demo orders exactly as in spec.md, idempotently,
all-or-nothing, deterministically, and only when `Seed:Enabled` is true

**Independent Test**: seed a fresh database, compare with spec.md "Seed Data"; seed again and see
no change

### Implementation for User Story 2

- [X] T018 [P] [US2] Create `SeedOptions` in `backend/src/Store.Infrastructure/Seeding/SeedOptions.cs` (`Enabled` default true, `AdminPassword`, `CustomerPassword`, `ImagesPath`), bound from the `Seed` configuration section
- [X] T019 [P] [US2] Create `backend/src/Store.Infrastructure/Seeding/Data/AccountSeedData.cs` with the two accounts from spec.md "Seed Data → Accounts" (email, full name, role, fixed demo phone); passwords are not here
- [X] T020 [P] [US2] Create `backend/src/Store.Infrastructure/Seeding/Data/CatalogSeedData.cs`: categories with parents and SKU codes, color SKU codes, the 18 products (number, name, slug, short description, price, featured, category, ordered color list, size range) and the six special stock values, all from spec.md "Seed Data"
- [X] T021 [P] [US2] Create `backend/src/Store.Infrastructure/Seeding/Data/DemoOrderSeedData.cs` with the 8 demo orders from spec.md "Demo orders" (number, days ago, status, items by product/color/size/quantity) and the fixed Cairo shipping address
- [X] T022 [US2] Implement `AccountSeeder` in `backend/src/Store.Infrastructure/Seeding/AccountSeeder.cs`, depending on `IAppDbContext` (not `AppDbContext`, constitution II): per account, skip if the email exists; otherwise require the password (throw `InvalidOperationException` naming `Seed:AdminPassword` / `Seed:CustomerPassword`), hash with `PasswordHasher<User>`, `CreatedAt` from `TimeProvider` (data-model.md User.PasswordHash, User.CreatedAt)
- [X] T023 [US2] Implement `CatalogSeeder` in `backend/src/Store.Infrastructure/Seeding/CatalogSeeder.cs`, depending on `IAppDbContext`: return if any product exists; require the demo customer (throw if missing); one own transaction via `IAppDbContext.Database` (research R4); build categories, products (`CreatedAt` = T − 60 days), variants with SKU `<category code>-<NNN>-<color code>-<size>` and stock = special value or the deterministic rule (research R9), images (`/images/products/<slug>-<color>.jpg`, `IsMain` true, `DisplayOrder` 1; log a warning with the file name only when the file is missing under `ImagesPath`; the row is inserted either way), orders via `Order.CreateHistorical` (`CreatedAt` = T − N days) with item snapshots and totals; the shipping fee is the literal historical value 50 from spec.md "Demo orders", on purpose, not the configured checkout fee (seeded totals must match the spec); commit
- [X] T024 [US2] Implement `DatabaseSeeder` in `backend/src/Store.Infrastructure/Seeding/DatabaseSeeder.cs`: `SeedAsync` returns immediately when `Enabled` is false (reads no other setting); otherwise runs `AccountSeeder` then `CatalogSeeder`
- [X] T025 [US2] Implement `DatabaseInitializer.InitializeAsync(bool isDevelopment, CancellationToken)` in `backend/src/Store.Infrastructure/Seeding/DatabaseInitializer.cs`, depending on `IAppDbContext`: Development → `MigrateAsync` then seed; otherwise seed only if `GetPendingMigrationsAsync()` is empty, else log one warning with the pending count and return (research R3, spec FR-018)
- [X] T026 [US2] Register `SeedOptions`, the seeders and `DatabaseInitializer` in `backend/src/Store.Infrastructure/DependencyInjection.cs`; call `InitializeAsync(app.Environment.IsDevelopment())` in `backend/src/Store.API/Program.cs`, setting `ImagesPath` to `<ContentRoot>/SeedImages` when not configured; add `Seed:Enabled: true` to `backend/src/Store.API/appsettings.json`; add a copy-to-output rule for `SeedImages/**/*.jpg` in `backend/src/Store.API/Store.API.csproj` and commit the 37 images the developer placed in `backend/src/Store.API/SeedImages/`

### Tests for User Story 2

- [X] T027 [US2] Write `backend/tests/Store.Infrastructure.Tests/SeedingTests.cs` (fixed seed time via `FixedTimeProvider`):
  - SC-002 exact counts (2 users, 12 categories, 18 products, 177 variants, 37 images, 8 orders, 12 order items)
  - all 18 products: price, featured flag and category match spec.md "Products" (US2-1)
  - spot checks: SKU `MTS-001-BLK-M`, the six special stock values, every order's subtotal/total/status/date offset, order-item snapshots, passwords stored as verifiable hashes
  - second run changes nothing (US2-2); missing `Seed:AdminPassword` throws naming the key and creates no user (US2-3); admin already present → only customer created (US2-5)
  - two fresh databases have identical stock for all 177 variants (SC-007)
  - rollback when a seed category slug already exists with no products, and when the demo customer is missing → no products/variants/images/orders saved (SC-008, research R5); products already present → catalog group skipped
  - `Enabled = false` with no passwords → completes and every runtime-seeded table is empty (SC-009, US2-7)
  - images: `ImagesPath` pointing to a missing folder → 37 warnings (file names only) and 37 image rows; a temp folder containing one of the files → 36 warnings, none for that file, still 37 rows (spec edge case "image file missing")
  - all green
- [X] T028 [US2] Write `backend/tests/Store.Infrastructure.Tests/InitializerTests.cs`: Development → migrates and seeds; non-Development with no pending migrations → seeds without migrating; non-Development on an empty database with no schema (`IRelationalDatabaseCreator.CreateAsync`) → no exception, exactly one warning containing the pending count (assert against `Database.GetMigrations().Count()`, not a literal), no migrations applied, no seed rows (research R3, spec FR-018); all green

**Checkpoint**: US1 + US2 pass; running the API in Development produces the seeded store

---

## Phase 5: User Story 3 - The database refuses invalid data (Priority: P2)

**Goal**: Prove every safety-net rule is enforced by SQL Server itself

**Independent Test**: each invalid write is rejected; each matching valid write succeeds

### Tests for User Story 3

- [X] T029 [US3] Write `backend/tests/Store.Infrastructure.Tests/ConstraintTests.cs`, sending statements to SQL Server with `ExecuteUpdateAsync`/`ExecuteDeleteAsync` or fresh contexts (research R7): stock −1 rejected, 0 accepted; cart/order item quantity 0 and 6 rejected, 1 and 5 accepted; price 0 rejected; duplicate product+color+size, duplicate SKU, email, slugs, order number, cart user+variant, order+variant rejected (FR-003/004); delete of an ordered variant or its product rejected with nothing removed; delete of a never-ordered product removes its variants and images; delete of an order removes its items; delete of a category with products or children rejected; delete of a user with orders rejected; delete of a user or variant removes their cart items (implementation already delivered by T012–T016)

### Implementation for User Story 3

- [X] T030 [US3] If T029 exposes a gap, fix the affected configuration in `backend/src/Store.Infrastructure/Persistence/Configurations/` and add a new named migration (never edit `InitialCreate` once applied elsewhere, constitution II); otherwise mark done with no code change and no commit

**Checkpoint**: US3 tests pass (SC-003)

---

## Phase 6: User Story 4 - Stock updates never silently overwrite each other (Priority: P2)

**Goal**: Concurrent stock updates on one variant are detected

**Independent Test**: two contexts update the same variant; one save succeeds, the other throws a
concurrency exception

### Tests for User Story 4

- [X] T031 [US4] Write `backend/tests/Store.Infrastructure.Tests/ConcurrencyTests.cs`: load the same variant in two `AppDbContext` instances, change `Stock` in both, save the first (succeeds), save the second (throws `DbUpdateConcurrencyException`); stored stock equals the first save's value (SC-004; data-model.md ProductVariant.RowVersion; implementation already delivered by T014/T016)

**Checkpoint**: US4 test passes

---

## Phase 7: User Story 5 - The admin sees correct dashboard statistics (Priority: P3)

**Goal**: `sp_GetDashboardStats` returns the contract's JSON in one call

**Independent Test**: call the procedure on the seeded database and compare with the contract's
expected results

### Implementation for User Story 5

- [X] T032 [US5] Add migration `AddDashboardStatsProcedure` in `backend/src/Store.Infrastructure/Persistence/Migrations/` (`dotnet ef migrations add AddDashboardStatsProcedure ...`): `Up` creates `dbo.sp_GetDashboardStats` with `migrationBuilder.Sql` exactly per [contracts/sp_GetDashboardStats.md](contracts/sp_GetDashboardStats.md) (parameters, half-open range, JSON built into an `nvarchar(max)` variable, `SELECT @json AS [Value]`, `JSON_QUERY(ISNULL(..., '[]'))` lists, ordering, no dynamic SQL; research R2); `Down` drops it; apply it to the local SQL Server

### Tests for User Story 5

- [X] T033 [US5] Write `backend/tests/Store.Infrastructure.Tests/DashboardStatsTests.cs` using the caller in [contracts/sp_GetDashboardStats.md](contracts/sp_GetDashboardStats.md) and a test-local record for the JSON: procedure exists after migration (US1-1); seeded range `[T − 30 days, T + 1 minute)`, threshold 3 → every expected value and list order in the contract (US5-1, SC-005); empty range → zeros, `bestSellers` `[]`, `lowStockVariants` unchanged (US5-2); omitted threshold → 3 (US5-3, call without `@LowStockThreshold`); boundary: an order exactly at `@FromUtc` counted, exactly at `@ToUtc` not counted; all green

**Checkpoint**: all user stories pass independently

---

## Phase 8: Polish & Cross-Cutting Concerns

- [X] T034 Run `dotnet test backend --settings backend/coverage.runsettings --collect:"XPlat Code Coverage"`; confirm all tests pass and changed non-excluded code (seeders, initializer, DbContext) has ≥ 80% line and branch coverage; report the numbers
- [X] T035 Run the [quickstart.md](quickstart.md) validation steps 1–4 against the local SQL Server and record the results
- [X] T036 Security/Data Access review of `backend/` (constitution Quality Gates): no `FromSqlRaw`/`ExecuteSqlRaw`/concatenated SQL, no secrets or connection strings in committed files, no data annotations on entities, seeders/initializer use `IAppDbContext` only, no leftover debug code or commented-out code

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)** → **Foundational (Phase 2)** → user stories → **Polish (Phase 8)**
- **US1 (Phase 3)** blocks every other story (all need the schema)
- **US2 (Phase 4)** depends on US1
- **US3 (Phase 5)** and **US4 (Phase 6)** depend on US1 only; they can run in parallel with US2
- **US5 (Phase 7)** depends on US1 and US2 (expected results come from the seed data)

### Within Each Story

- Implementation first, then the story's test task; every commit builds
- Seed data classes (T019–T021) before seeders (T022–T023), seeders before `DatabaseSeeder`
  (T024), then `DatabaseInitializer` (T025), then wiring (T026), then tests (T027–T028)

### Parallel Opportunities

- T003 alongside T002
- T004–T007 together (different files)
- T012–T015 together (different configuration files)
- T018–T021 together
- After US1: US3 (T029–T030) and US4 (T031) in parallel with US2

---

## Parallel Example: User Story 2

```text
Task: "Create SeedOptions in backend/src/Store.Infrastructure/Seeding/SeedOptions.cs"
Task: "Create AccountSeedData in backend/src/Store.Infrastructure/Seeding/Data/AccountSeedData.cs"
Task: "Create CatalogSeedData in backend/src/Store.Infrastructure/Seeding/Data/CatalogSeedData.cs"
Task: "Create DemoOrderSeedData in backend/src/Store.Infrastructure/Seeding/Data/DemoOrderSeedData.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1)

1. Phase 1 + Phase 2
2. Phase 3 (US1): schema with all rules
3. Validate with `SchemaTests`

### Incremental Delivery

1. US1 → schema
2. US2 → seeded store on startup (demo-ready)
3. US3 + US4 → safety-net and concurrency proof
4. US5 → dashboard procedure
5. Polish → coverage, quickstart, security review

---

## Notes

- Critical areas (constitution "AI-Assisted Development"): variants/stock (T014, T023) and
  concurrency (T031) need an explanation of the implementation alongside the tests.
- Migrations are applied only to the local SQL Server; any other database needs approval.
- Seed images: 37 existing files, placed by the developer after T001 and committed in T026.

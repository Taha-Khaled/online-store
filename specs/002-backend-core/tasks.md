---

description: "Task list for 002-backend-core: API Foundation, Authentication & Public Catalog"
---

# Tasks: Backend Core — API Foundation, Authentication & Public Catalog

**Input**: Design documents from `/specs/002-backend-core/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md),
[data-model.md](data-model.md), [contracts/api.md](contracts/api.md), [quickstart.md](quickstart.md)

**Tests**: Required (developer's rules, constitution Quality Gates, 80% coverage). Within each story
the implementation task comes first and its test task follows, so every commit builds and every
code change is covered by the next task at the latest.

**Work items**: each task ID is the work item, written `#002-<task>`
(e.g. `feat #002-T013 add product list service`). Exactly one task per commit; never group tasks.
A task that ends with no code change produces no commit. The PR into `develop` includes
`Closes #5`. `tasks.md` `[X]` marks are committed once at the end (`docs #002 ...`).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: User story from spec.md (US1–US5)
- Paths are relative to the repository root; backend code lives under `backend/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Packages, the new test project, coverage settings

- [ ] T001 Add packages, pinned exactly (research R14): `FluentValidation.DependencyInjectionExtensions` (latest 12.x) in `backend/src/Store.Application/Store.Application.csproj`; `Microsoft.AspNetCore.Authentication.JwtBearer` (10.0.x, same patch line as the installed ASP.NET Core / EF packages) in `backend/src/Store.Infrastructure/Store.Infrastructure.csproj`; `Swashbuckle.AspNetCore` (newest stable supporting .NET 10, research R12) in `backend/src/Store.API/Store.API.csproj`; build with zero warnings
- [ ] T002 Create `backend/tests/Store.API.Tests` (xUnit, `net10.0`, `IsPackable=false`, global `using Xunit`) with `xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`, `Shouldly`, `coverlet.collector` (same versions as the 001 test projects) and `Microsoft.AspNetCore.Mvc.Testing` (10.0.x, same patch as JwtBearer); references `Store.API` and `Store.Infrastructure`; add it to `backend/Store.slnx`; no placeholder tests
- [ ] T003 [P] Extend `ExcludeByFile` in `backend/coverage.runsettings` with `**/*Request.cs` and `**/*Response.cs` (DTO records without logic, constitution Project Workflow; plan.md "Coverage exclusions")

**Checkpoint**: `dotnet build backend` succeeds with zero warnings; 001 tests still pass

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Error contract, validation plumbing, API test fixture — used by every story

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [ ] T004 [P] Create `backend/src/Store.Application/Common/Errors/`: `ErrorCodes.cs` (every code in [contracts/api.md](contracts/api.md) "Error shape" as `const string`), and `AppException` base (code + message) with `ValidationException` (adds `IReadOnlyDictionary<string, string[]> Errors`, code `validationFailed`), `NotFoundException`, `ConflictException`, `UnauthorizedException` (research R6); no HTTP types
- [ ] T005 [P] Create `backend/src/Store.Application/Common/Validation/ValidationExtensions.cs` (`ValidateOrThrowAsync<T>(this IValidator<T>, T, CancellationToken)` → throws `ValidationException` with camelCase field names, research R7), `backend/src/Store.Application/Common/PagedResponse.cs` (`Items`, `Page`, `PageSize`, `TotalCount`, `TotalPages`) and `backend/src/Store.Application/Common/CatalogLimits.cs` (`MaxQuantityPerVariant = 5`)
- [ ] T006 Create `AddApplication(IServiceCollection)` in `backend/src/Store.Application/DependencyInjection.cs` registering validators with `AddValidatorsFromAssembly` (services are added by the tasks that create them); call it from `backend/src/Store.API/Program.cs`
- [ ] T007 Create the API test fixtures in `backend/tests/Store.API.Tests/Fixtures/`: `MutableTimeProvider.cs` (`TimeProvider` with settable now and `Advance(TimeSpan)`, starting at 2026-11-24 12:00 UTC like the 001 seed time), `StoreApiFactory.cs` (`WebApplicationFactory<Program>, IAsyncLifetime`: creates `StoreTests_<guid>` on `STORE_TEST_SQLSERVER` and fails with a message naming it when missing; environment `Development`; `UseSetting` for `ConnectionStrings:Store`, `Seed:AdminPassword`, `Seed:CustomerPassword`, `Jwt:Key` (≥ 32 bytes, test-only), `Jwt:Issuer`, `Jwt:Audience`; replaces the `TimeProvider` singleton with one shared `MutableTimeProvider`; exposes `Time`, `CreateDbContext()`, seeded passwords; drops the database on dispose — research R13), `HttpExtensions.cs` (read JSON as test-local records, read the error shape); add `public partial class Program;` to `backend/src/Store.API/Program.cs` only if the build requires it
- [ ] T008 Implement the error pipeline in `backend/src/Store.API/` (research R6, FR-001–FR-004): `Errors/ErrorResponse.cs`; `Errors/ApiExceptionHandler.cs` (`IExceptionHandler`: `ValidationException` → 400 with `errors`, `UnauthorizedException` → 401, `NotFoundException` → 404, `ConflictException` → 409, anything else → 500 `internalError` with a generic message; logs exception type, message, trace id and route only); `Errors/ErrorResponseWriter.cs` used by `UseStatusCodePages` for body-less 404 `notFound` / 405 `methodNotAllowed` / 415 `unsupportedMediaType` / other `requestFailed`; in `Program.cs`: `AddControllers` with `SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true`, `InvalidModelStateResponseFactory` → 400 `validationFailed` with camelCase field errors, camelCase JSON with null `errors` omitted, `AddExceptionHandler<ApiExceptionHandler>`, `UseExceptionHandler(_ => { })` (no ProblemDetails; the empty pipeline is needed when no `IProblemDetailsService` is registered), `UseStatusCodePages`, `MapControllers`
- [ ] T009 Write `backend/tests/Store.API.Tests/ErrorShapeTests.cs` with a test-only controller in `backend/tests/Store.API.Tests/Fixtures/ErrorTestController.cs` added through `ConfigureTestServices` (`AddApplicationPart`): each Application exception → its status, code and message; `ValidationException` → `errors` present; unexpected exception → 500 `internalError`, generic message, exception text absent from the body; bad `int` query value and malformed JSON body → 400 `validationFailed` with the field; unknown route → 404 `notFound`; wrong method → 405; non-JSON body → 415; every response has `code` + `message` (SC-004)

**Checkpoint**: error pipeline proven over HTTP; fixture starts the real app on a seeded test database

---

## Phase 3: User Story 1 - Shopper browses the catalog (Priority: P1) 🎯 MVP

**Goal**: category tree, product list with search/filters/sort/paging, filter panel, images

**Independent Test**: call `/api/categories`, `/api/products`, `/api/products/filters` and an
image URL on the seeded store; check counts, order and shapes against the spec and
[contracts/api.md](contracts/api.md)

### Implementation for User Story 1

- [ ] T010 [US1] Create `backend/src/Store.Application/Categories/` (`CategoryResponse.cs` with children, `CategoryService.cs`: `AsNoTracking` + `Select`, parents and children ordered by `Id`, FR-031) and `backend/src/Store.API/Controllers/CategoriesController.cs` (`GET /api/categories`); register the service in `AddApplication`
- [ ] T011 [US1] Write `backend/tests/Store.API.Tests/CategoriesTests.cs`: 3 parents with 3 children each, names and slugs from the 001 seed (AC 4)
- [ ] T012 [US1] Create `backend/src/Store.Application/Products/ProductSort.cs` (`Newest`, `PriceAsc`, `PriceDesc`, `NameAsc`), `ProductListQuery.cs` (search, category, colors, sizes, minPrice, maxPrice, featured, sort, page = 1, pageSize = 12), `ProductListQueryValidator.cs` (research R7: page ≥ 1, pageSize 1–48, search ≤ 100, prices ≥ 0, min ≤ max, sort `IsInEnum`), and `backend/src/Store.API/Binding/ProductSortModelBinder.cs` + provider (names only, case-insensitive, numbers rejected → model state error on `sort`, research R8)
- [ ] T013 [US1] Create `backend/src/Store.Application/Products/ProductQueryExtensions.cs` as flat public static helpers on `IQueryable<Product>`: `ApplySearch` (lowercased `Contains` on name, description, category name, parent category name — research R9, FR-032), `ApplyVariantFilters` (one in-stock variant matching requested colors AND sizes, lowercased names — FR-033, FR-035), `ApplySort` (fixed chains incl. tiebreakers — research R8, FR-036)
- [ ] T014 [US1] Create `backend/src/Store.Application/Products/ProductService.cs` `ListAsync` and DTOs `ProductListItem.cs` / `ColorSummaryResponse.cs` (research R10): validate, resolve category slug + children (unknown → `NotFoundException(categoryNotFound)`), `IsActive`, featured, price range, `ApplySearch`, `ApplyVariantFilters`, `CountAsync`, `ApplySort`, `Skip/Take`, project with main image (`IsMain`, lowest `DisplayOrder`, then `Id`) and `inStock`; second query for the page's colors ordered by their first image (FR-038, FR-039); `TotalPages` = ceil(total / pageSize); register in `AddApplication`; add `GET /api/products` (`[FromQuery]`, repeatable `color` / `size`) in `backend/src/Store.API/Controllers/ProductsController.cs`
- [ ] T015 [US1] Add `ProductService.GetFiltersAsync(string? category)` with `ProductFiltersResponse.cs` (colors by name, sizes by display order from in-stock variants on active products in scope; min/max price of active products in scope, null when none; unknown category → 404 — FR-041) and `GET /api/products/filters` in `ProductsController` (literal route takes precedence over `{slug}`)
- [ ] T016 [US1] Serve product images (research R11, FR-007): `backend/src/Store.API/Extensions/ProductImagesExtensions.cs` with `UseProductImages(WebApplication)` → `UseStaticFiles` with a `PhysicalFileProvider` on `SeedOptions.ImagesPath`, `RequestPath = "/images/products"`, no directory browsing; call it in `Program.cs` before routing

### Tests for User Story 1

- [ ] T017 [US1] Write `backend/tests/Store.API.Tests/ProductListTests.cs`: defaults → 18 total, 12 + 6, newest first (AC 5); `featured=true` 6, `category=shoes` 6, `category=shoes-sneakers` 2 (AC 6); `priceAsc` first Basic Ribbed Top, `priceDesc` first Leather Derby Shoes, `nameAsc` order (AC 7); page beyond last → empty with totals; unknown category → 404 `categoryNotFound`; `color=BLACK` = `color=black`, `size=xl` = `size=XL`, multiple colors OR; color+size on the same variant only (e.g. `color=white&size=l` excludes Classic Cotton T-Shirt whose White/L is out of stock, while each filter alone includes it); unknown color → empty; min/max price range; list item fields and colors order; `sort=random`, `pageSize=49`, `page=0`, `minPrice>maxPrice`, 101-char search → 400 `validationFailed` with the field (AC 10)
- [ ] T018 [P] [US1] Write `backend/tests/Store.API.Tests/ProductSearchTests.cs` (search `shoes` → all 6 shoe products; `shoes` + `size=42` → 5 without Everyday White Sneakers (AC 16); `JEANS` = `jeans` (AC 17); `%`, `_`, `[` → 0 results; whitespace search ignored; search combined with filters) and `backend/tests/Store.Infrastructure.Tests/Catalog/ProductQueryExtensionsTests.cs` (`ApplySearch("50%_[x").ToQueryString()` uses `LIKE` with `ESCAPE`, research R9)
- [ ] T019 [P] [US1] Write `backend/tests/Store.API.Tests/ProductPagingTests.cs` (page size 5 through every sort → each of the 18 products exactly once, AC 18 / SC-003), `backend/tests/Store.API.Tests/FilterPanelTests.cs` (all scope vs `category=shoes`: sizes only shoe sizes, prices 400–2200; unknown category 404) and `backend/tests/Store.API.Tests/ImagesTests.cs` (`/images/products/classic-cotton-t-shirt-black.jpg` → 200 `image/jpeg` without a token (AC 11); missing file → 404 `notFound`; `..` path not served)

**Checkpoint**: US1 tests pass; catalog browsable from HTTP

---

## Phase 4: User Story 2 - Shopper views a product's details (Priority: P1)

**Goal**: product details by slug with colors, images, sizes and variant availability

**Independent Test**: request `classic-cotton-t-shirt`, an unknown slug and a deactivated product

### Implementation for User Story 2

- [ ] T020 [US2] Add `ProductService.GetBySlugAsync` with `ProductDetailsResponse.cs` (category name/slug/parent name, colors with images ordered by display order then id, sizes by display order, variants with `inStock` and `maxQuantity = Math.Min(Stock, CatalogLimits.MaxQuantityPerVariant)` computed in the projection; no stock field anywhere; unknown or inactive → `NotFoundException(productNotFound)` — FR-040) and `GET /api/products/{slug}` in `ProductsController`

### Tests for User Story 2

- [ ] T021 [US2] Write `backend/tests/Store.API.Tests/ProductDetailsTests.cs`: `classic-cotton-t-shirt` → 3 colors, sizes S–XL in order, 12 variants, White/L `inStock` false and `maxQuantity` 0, Black/M `maxQuantity` 1, all stock 8–20 variants `maxQuantity` 5 (AC 8); raw JSON has no `stock` property (SC-008); unknown slug → 404 `productNotFound`
- [ ] T022 [US2] Write `backend/tests/Store.API.Tests/InactiveProductTests.cs` (own factory): deactivate one product directly in the test database (`ExecuteUpdateAsync` on `IsActive`), then it is missing from the list (total 17), its details return 404 `productNotFound`, and it no longer counts in the filter panel (AC 9, FR-030)

**Checkpoint**: US1 + US2 pass — public catalog complete

---

## Phase 5: User Story 3 - Customer registers and logs in (Priority: P2)

**Goal**: password hasher, refresh token storage, JWT issue/validation, register/login/me

**Independent Test**: register, log in with seeded accounts, call `/api/auth/me` with and without
a token and after 15 minutes

### Implementation for User Story 3

- [ ] T023 [US3] Create `backend/src/Store.Application/Common/Security/IPasswordHasher.cs` and `backend/src/Store.Infrastructure/Security/PasswordHasher.cs` (wraps `PasswordHasher<object>` with a private static placeholder; `Verify` accepts `Success` and `SuccessRehashNeeded` — research R1, FR-026); register in `AddInfrastructure`
- [ ] T024 [US3] Write `backend/tests/Store.Infrastructure.Tests/Security/PasswordHasherTests.cs`: hash ≠ password and verifies; wrong password fails; two hashes of the same password differ; a hash made by `PasswordHasher<User>` (001 format) verifies (AC 12)
- [ ] T025 [US3] Update `backend/src/Store.Infrastructure/Seeding/AccountSeeder.cs` to take `IPasswordHasher` and pass `hasher.Hash(password)` to the `User` constructor (no empty hash, no `SetPasswordHash` call); update `backend/tests/Store.Infrastructure.Tests/Fixtures/Seeders.cs` to pass `new PasswordHasher()`; 001 `SeedingTests` stay green (FR-027)
- [ ] T026 [US3] In `backend/src/Store.Domain/Entities/User.cs` validate `passwordHash` in the constructor (data-model.md User.PasswordHash: not null/empty/whitespace → `ArgumentException`) and remove `SetPasswordHash`; update `backend/tests/Store.Domain.Tests/Entities/UserTests.cs` (valid hash accepted; null/empty/whitespace rejected; `SetPasswordHash` tests removed)
- [ ] T027 [US3] Create `backend/src/Store.Domain/Entities/RefreshToken.cs` per data-model.md "Domain behavior": data-model.md RefreshToken.Id, RefreshToken.UserId, RefreshToken.TokenHash, RefreshToken.CreatedAt, RefreshToken.ExpiresAt, RefreshToken.RevokedAt, RefreshToken.ReplacedByTokenHash, navigation `User`; constructor checks; `IsActive(now)`, `IsRotated`, `Revoke(now)`, `MarkRotated(now, replacedByHash)`
- [ ] T028 [US3] Write `backend/tests/Store.Domain.Tests/Entities/RefreshTokenTests.cs`: constructor guards; active before expiry, inactive at exactly `ExpiresAt` and after revoke; `Revoke` keeps the first time; `MarkRotated` sets both fields, rejects inactive tokens and empty hashes; `IsRotated` only after rotation
- [ ] T029 [US3] Persist refresh tokens: `backend/src/Store.Infrastructure/Persistence/Configurations/RefreshTokenConfiguration.cs` (table `RefreshTokens`; data-model.md RefreshToken.TokenHash and RefreshToken.ReplacedByTokenHash as `varchar(64)`, unique index on TokenHash, index on UserId, RefreshToken.RevokedAt as concurrency token); relationship in `UserConfiguration.cs` (cascade); `DbSet<RefreshToken>` in `IAppDbContext.cs` and `AppDbContext.cs`; migration `AddRefreshTokens` in `backend/src/Store.Infrastructure/Persistence/Migrations/` (`dotnet ef migrations add AddRefreshTokens ...`, snapshot updated); apply to the local SQL Server
- [ ] T030 [US3] Update `backend/tests/Store.Infrastructure.Tests/SchemaTests.cs` for the `RefreshTokens` table: table and columns exist, unique `IX_RefreshTokens_TokenHash`, `IX_RefreshTokens_UserId`, FK to `Users` with cascade; full 001 suite green (AC 20, FR-042)
- [ ] T031 [US3] Create `backend/src/Store.Infrastructure/Security/JwtOptions.cs` (`Key`, `Issuer`, `Audience`, `SectionName = "Jwt"`, `AccessTokenLifetime` 15 min, `RefreshTokenLifetime` 7 days) and `JwtOptionsValidator.cs` (`IValidateOptions<JwtOptions>`, one failure per problem naming the key; key < 32 UTF-8 bytes rejected — research R3, FR-009); register with `ValidateOnStart()` in `AddInfrastructure`; in `Program.cs` read `IOptions<JwtOptions>.Value` right after `Build()`, before database initialization
- [ ] T032 [US3] Write `backend/tests/Store.Infrastructure.Tests/Security/JwtOptionsValidatorTests.cs`: valid options pass; each missing key and a 31-byte key fail with messages naming `Jwt:Key` / `Jwt:Issuer` / `Jwt:Audience`; several problems reported together
- [ ] T033 [US3] Create `backend/src/Store.Application/Common/Security/ITokenService.cs` (`CreateAccessToken(User)` → token + expiry; `CreateRefreshToken()` → token + hash; `HashRefreshToken(string)`) and `backend/src/Store.Infrastructure/Security/TokenService.cs` (research R2, R4: `JsonWebTokenHandler`, HMAC-SHA256, claims `sub`/`email`/`role`/`jti`, times from `TimeProvider`; 64 random bytes `Base64Url`; SHA-256 uppercase hex); register in `AddInfrastructure`
- [ ] T034 [US3] Write `backend/tests/Store.Infrastructure.Tests/Security/TokenServiceTests.cs`: access token claims, issuer, audience, `exp` = now + 15 min, signature validates with the key; refresh tokens unique, 86 URL-safe chars; hash is 64 hex chars, deterministic, differs from the token
- [ ] T035 [US3] Create `backend/src/Store.API/Extensions/JwtAuthenticationExtensions.cs` (`AddJwtAuthentication`: validate issuer, audience, signing key, lifetime; `ClockSkew = 0`; `MapInboundClaims = false`; `RoleClaimType = "role"`, `NameClaimType = "sub"`; lifetime validator using the injected `TimeProvider` (`notBefore <= now < expires`); `OnChallenge` → `HandleResponse` + 401 `unauthorized` shape; `OnForbidden` → 403 `forbidden` shape — research R2, R6, FR-017, FR-018); `UseAuthentication` / `UseAuthorization` in `Program.cs`
- [ ] T036 [US3] Create `backend/src/Store.Application/Auth/`: `RegisterRequest.cs`, `LoginRequest.cs`, `AuthResponse.cs`, `UserResponse.cs` (fields per [contracts/api.md](contracts/api.md)), `RegisterRequestValidator.cs` (FR-013, research R7), `LoginRequestValidator.cs` (presence only, FR-014), `AuthService.cs` with `RegisterAsync` (normalize, validate, `AnyAsync` → 409, hash, `Role.Customer`, save, on `DbUpdateException` re-check email → 409 else rethrow — research R5; issue tokens + `RefreshToken` row), `LoginAsync` (lowercased email; unknown email runs a dummy `Verify`; same `UnauthorizedException(invalidCredentials)` for both failures — FR-015), `GetCurrentUserAsync(int userId)` (missing user → `UnauthorizedException(unauthorized)`); logs user id + outcome only (FR-004); register in `AddApplication`; `backend/src/Store.API/Controllers/AuthController.cs` with `POST register` (201), `POST login`, `GET me` (`[Authorize]`, user id from `sub`)

### Tests for User Story 3

- [ ] T037 [US3] Write `backend/tests/Store.API.Tests/RegisterTests.cs`: valid → 201, role Customer, email stored lowercase, access token works on `/me`, and a `RefreshTokens` row holds the hash of the returned refresh token (its use on `/refresh` is covered in T041) (AC 1); same email in another case → 409 `emailAlreadyExists`; empty email → 400 with `errors.email` (AC 10); each FR-013 rule at its boundaries (name 1/2/100/101, phone with `+`, letters, 21 chars, password without digit / letter / 7 chars); body never echoed in the response
- [ ] T038 [US3] Write `backend/tests/Store.API.Tests/LoginTests.cs` and `backend/tests/Store.API.Tests/MeTests.cs`: login `customer@store.local` and `admin@store.local` with seeded passwords (AC 2, AC 12); email in upper case logs in; wrong password and unknown email → identical 401 `invalidCredentials` bodies (AC 3); empty fields → 400; `/me` → Demo Customer / Customer (AC 2); no token, garbage token, token signed with another key, wrong issuer, wrong audience → 401 `unauthorized`; time + 14:59 → 200, + 15:00 → 401 (AC 2, AC 19); user deleted from the database → 401
- [ ] T039 [US3] Write `backend/tests/Store.Infrastructure.Tests/Auth/RegisterRaceTests.cs`: build `AuthService` directly on a test database with a `SaveChangesInterceptor` that inserts the same email through a second context just before the save → `ConflictException(emailAlreadyExists)`, not a `DbUpdateException` (research R5); a non-email `DbUpdateException` is rethrown

**Checkpoint**: US3 passes; register/login/me usable

---

## Phase 6: User Story 4 - Customer stays logged in safely (Priority: P2)

**Goal**: refresh rotation, reuse detection, per-device logout

**Independent Test**: log in, refresh, reuse tokens, log out, advance time 7 days, inspect
`RefreshTokens`

### Implementation for User Story 4

- [ ] T040 [US4] Add to `backend/src/Store.Application/Auth/`: `RefreshRequest.cs`, `RefreshRequestValidator.cs` (token not empty), `AuthService.RefreshAsync` (hash → tracked load; unknown / expired / logged-out → `UnauthorizedException(invalidRefreshToken)`; rotated → revoke all the user's active tokens with `Revoke(now)` → 401; active → `MarkRotated` + new `RefreshToken` + save in one transaction; `DbUpdateConcurrencyException` → roll back, reload, reuse handling, 401 — research R4, FR-022, FR-023, FR-028) and `AuthService.LogoutAsync` (revoke only if `IsActive(now)`; never throws for unknown tokens — FR-024, FR-029); logs token row id / user id only; `POST refresh` and `POST logout` (204) in `AuthController`

### Tests for User Story 4

- [ ] T041 [US4] Write `backend/tests/Store.API.Tests/RefreshTokenTests.cs` (fresh registered user per test): the refresh token returned by registration refreshes immediately (AC 1); refresh → new pair that works, old token → 401 `invalidRefreshToken` (AC 13); reusing the rotated token → 401 and the newest token is revoked too (AC 13, SC-006); logged-out token → 401 and other tokens still work (FR-028); time + 7 days → 401, + 7 days − 1 s → 200 (AC 15); malformed / unknown token → same 401 body; after several logins and refreshes no returned refresh token appears in `RefreshTokens` (any column) and every `TokenHash` is 64 hex chars (AC 15, SC-005)
- [ ] T042 [P] [US4] Write `backend/tests/Store.API.Tests/LogoutTests.cs`: two logins → logout first → first 401, second works; same with the second checked first (AC 14); logout with unknown, expired, already revoked and rotated tokens → 204 and no other token revoked (FR-029); empty token → 400; access token still works on `/me` after logout (FR-025)
- [ ] T043 [P] [US4] Write `backend/tests/Store.API.Tests/RefreshRaceTests.cs`: 10 parallel refreshes with the same token → exactly one 200, the rest 401; then assert explicitly that the winner's new refresh token returns 401 `invalidRefreshToken` and its `RefreshTokens` row has `RevokedAt` set, because the losers presented a rotated token and triggered reuse detection — the test name and a comment document this as intended behavior (research R4, FR-023)

**Checkpoint**: US4 passes; sessions are safe

---

## Phase 7: User Story 5 - Developer runs and explores the API (Priority: P3)

**Goal**: Swagger in Development with Bearer support; startup refuses bad JWT settings

**Independent Test**: start with missing/short JWT settings; open Swagger in Development and
Production

### Implementation for User Story 5

- [ ] T044 [US5] Create `backend/src/Store.API/Extensions/SwaggerExtensions.cs` (`AddSwagger` with one `Bearer` HTTP scheme, JWT format, applied to `[Authorize]` actions — research R12, FR-006) and map Swagger UI in `Program.cs` only when `IsDevelopment()`

### Tests for User Story 5

- [ ] T045 [US5] Write `backend/tests/Store.API.Tests/StartupTests.cs`: factory with each of `Jwt:Key` / `Jwt:Issuer` / `Jwt:Audience` empty, and with a 31-byte key → starting the host throws `OptionsValidationException` whose message names the key, and the test database is not created (US5-1); Development → `/swagger/v1/swagger.json` 200 with a `Bearer` scheme (US5-2); Production on a database migrated by the test → `/swagger/v1/swagger.json` 404 `notFound`

**Checkpoint**: all user stories pass independently

---

## Phase 8: Polish & Cross-Cutting Concerns

- [ ] T046 Run `dotnet test backend --settings backend/coverage.runsettings --collect:"XPlat Code Coverage"`; all tests pass; merge the per-project cobertura files with a scratch PowerShell script (union of line/branch hits, nothing committed, no new tool) and confirm ≥ 80% line and branch coverage on changed non-excluded files; report the numbers
- [ ] T047 Run the [quickstart.md](quickstart.md) validation steps 1–6 against the local SQL Server (applies `AddRefreshTokens` to the local `OnlineStore` database) and record the results
- [ ] T048 Security/Data Access review of `backend/` (constitution Quality Gates): no raw SQL APIs, no `DateTime.Now` / `UtcNow` / `DateTimeOffset.Now` outside tests, no secrets or JWT keys in committed files, no cookies set, read-only queries `AsNoTracking` + `Select`, logs contain no bodies / tokens / emails / names, controllers contain no business rules, no debug or commented-out code

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)** → **Foundational (Phase 2)** → user stories → **Polish (Phase 8)**
- **US1 (Phase 3)** and **US3 (Phase 5)** depend only on Foundational
- **US2 (Phase 4)** depends on US1 (`ProductService`, `ProductsController`)
- **US4 (Phase 6)** depends on US3 (tokens, `AuthService`, `RefreshTokens`)
- **US5 (Phase 7)** depends on US3 (JWT options and Bearer scheme exist)

### Within Each Story

- Implementation first, then the story's tests; every commit builds
- US3 order matters for a green build: hasher (T023) → seeder uses it (T025) → `User` rejects empty
  hashes (T026); entity (T027) → persistence + migration (T029) → options (T031) → token service
  (T033) → auth pipeline (T035) → service + controller (T036)

### Parallel Opportunities

- T003 alongside T001–T002; T004 and T005 together
- US1 (Phases 3–4) and US3 (Phase 5) in parallel after Phase 2 (different files except
  `Program.cs` / `AddApplication` registrations — merge carefully)
- T018 and T019 together; T042 and T043 together

---

## Parallel Example: User Story 1 tests

```text
Task: "ProductSearchTests + ProductQueryExtensionsTests (T018)"
Task: "ProductPagingTests + FilterPanelTests + ImagesTests (T019)"
```

---

## Implementation Strategy

### MVP First (User Story 1)

1. Phase 1 + Phase 2
2. Phase 3 (US1): browsable catalog with images
3. Validate with US1 tests

### Incremental Delivery

1. US1 + US2 → public catalog (frontend can start catalog pages)
2. US3 → accounts and login
3. US4 → safe sessions
4. US5 → Swagger + startup guard
5. Polish → coverage, quickstart, security review

---

## Notes

- Critical areas (constitution "AI-Assisted Development"): variant availability (T014, T020)
  needs an explanation of `inStock` / `maxQuantity` alongside its tests.
- Migrations are applied only to the local SQL Server; the hosted database needs approval.
- Each API test class gets its own seeded database; tests that change shared state use their own
  class or a freshly registered user (plan.md "Assumption").

# Implementation Plan: Backend Core — API Foundation, Authentication & Public Catalog

**Branch**: `feat/002-backend-core` | **Date**: 2026-11-25 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/002-backend-core/spec.md`

**Tracking**: GitHub issue #5. Commits `<type> #002-T0xx <summary>`, one task per commit; the PR
into `develop` includes `Closes #5`.

## Summary

Turn the 001 database into a usable API: one error shape for every failure, JWT access tokens
(15 min, zero skew) with rotating, hashed refresh tokens (7 days, reuse detection), customer
registration and login through an application-level `IPasswordHasher`, and a public catalog
(category tree, product list with search/filters/sort/paging, product details, filter panel) as
`AsNoTracking` LINQ projections. Product images are served as static files. One new migration,
`AddRefreshTokens`. Verified by domain, infrastructure and `WebApplicationFactory` tests on the
local SQL Server.

## Technical Context

**Language/Version**: C# 14 on .NET 10 (as 001)

**Primary Dependencies**: ASP.NET Core Web API; EF Core 10.0.15; `FluentValidation.DependencyInjectionExtensions`;
`Microsoft.AspNetCore.Authentication.JwtBearer`; `Swashbuckle.AspNetCore` — all approved
([research R14](research.md#r14-packages-all-on-the-constitutions-approved-list))

**Storage**: SQL Server (local `localhost\MSSQLSERVER01`); new table `RefreshTokens`

**Testing**: xUnit, Shouldly, coverlet; new `Store.API.Tests` with
`Microsoft.AspNetCore.Mvc.Testing`; one seeded database per test class
([research R13](research.md#r13-api-integration-tests))

**Target Platform**: ASP.NET Core Web API on Windows/IIS (MonsterASP.NET)

**Project Type**: web service (backend only)

**Performance Goals**: catalog responses well under 1 s locally (SC-007); one count + one page
query + one colors query per list request

**Constraints**: no raw SQL; no `DateTime.Now`/`UtcNow` (only `TimeProvider`); no secrets in
files; logs metadata only; every error in one shape

**Scale/Scope**: 9 endpoints + static images; 18 seeded products; 1 new table

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle / rule | Status | How |
|---|---|---|
| I. Four projects; Domain dependency-free | ✅ | `RefreshToken` in Domain, no packages; new test project only |
| I. Rules in Domain/Application, not controllers | ✅ | controllers only bind and call services; token rules in `RefreshToken`, auth rules in `AuthService` |
| I. DB constraints are a safety net, not the only rule | ✅ | email uniqueness checked in service + unique index; token hash unique index |
| I. No MediatR/CQRS/events; feature folders with DTOs, service, validators | ✅ | `Auth/`, `Categories/`, `Products/` |
| II. EF Core via `IAppDbContext`; no repository | ✅ | `DbSet<RefreshToken>` added to `IAppDbContext` |
| II. Fluent API in separate classes | ✅ | `RefreshTokenConfiguration` |
| II. Read-only queries `AsNoTracking` + `Select` to DTOs | ✅ | [research R10](research.md#r10-catalog-queries) |
| II. Named migration for every schema change | ✅ | `AddRefreshTokens` |
| III. LINQ only, no raw SQL | ✅ | search via `Contains` (parameterized, escaped) ([R9](research.md#r9-case-insensitive-wildcard-safe-search)) |
| III. Sort/filter options from enums | ✅ | `ProductSort` enum + strict binder ([R8](research.md#r8-sorting-and-query-binding)); color/size values are parameters |
| III. ASP.NET Core `PasswordHasher` | ✅ | wrapped by `IPasswordHasher` ([R1](research.md#r1-password-hasher-abstraction)) |
| III. JWT from Bearer header only; no cookies | ✅ | JwtBearer default; no cookie code |
| III. Role authorization on admin endpoints | n/a | no admin endpoints in this feature; role claim mapped for 004 |
| III. Browser calls API only for images | ✅ | static files at `/images/products` |
| III. Secrets from User Secrets / env vars | ✅ | `Jwt:*`; startup validation names missing keys ([R3](research.md#r3-jwt-configuration-and-startup-validation)) |
| IV. Stock per variant; never exposed beyond need | ✅ | `inStock` / `maxQuantity` only |
| V. UTC dates | ✅ | `TimeProvider`; 001 UTC converter |
| Business rule: max 5 per variant | ✅ | `maxQuantity = min(stock, 5)` from one constant |
| API Conventions: one error shape, codes as constants, FluentValidation, global handler, Swagger in Dev | ✅ | [R6](research.md#r6-one-error-shape-for-every-failure), [R7](research.md#r7-validation), [R12](research.md#r12-swagger) |
| Workflow: approved dependencies only | ✅ | R14 |
| Workflow: 80% coverage with exclusions | ✅ | tests per layer; exclusions unchanged + DTOs without logic |
| Workflow: backend only | ✅ | no frontend files |

**Result**: PASS (constitution 1.7.1). Re-checked after Phase 1: no change.

## Project Structure

### Documentation (this feature)

```text
specs/002-backend-core/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/api.md
├── checklists/requirements.md
└── tasks.md              # created by /speckit-tasks
```

### Source Code (new ✚ / changed ✎)

```text
backend/
├── src/
│   ├── Store.Domain/Entities/
│   │   ├── RefreshToken.cs                 ✚ IsActive, IsRotated, Revoke, MarkRotated
│   │   └── User.cs                         ✎ hash validated in ctor; SetPasswordHash removed
│   ├── Store.Application/
│   │   ├── Common/
│   │   │   ├── IAppDbContext.cs            ✎ + RefreshTokens
│   │   │   ├── Errors/                     ✚ ErrorCodes, ValidationException, NotFoundException,
│   │   │   │                                  ConflictException, UnauthorizedException
│   │   │   ├── Security/                   ✚ IPasswordHasher, ITokenService
│   │   │   ├── Validation/                 ✚ ValidateOrThrowAsync extension (validator → ValidationException)
│   │   │   ├── PagedResponse.cs            ✚
│   │   │   └── CatalogLimits.cs            ✚ MaxQuantityPerVariant = 5
│   │   ├── Auth/                           ✚ AuthService, requests/responses, validators
│   │   ├── Categories/                     ✚ CategoryService, CategoryResponse
│   │   ├── Products/                       ✚ ProductService, ProductSort, queries, DTOs, validators
│   │   └── DependencyInjection.cs          ✚ AddApplication (services + validators)
│   ├── Store.Infrastructure/
│   │   ├── Security/                       ✚ PasswordHasher, TokenService, JwtOptions, JwtOptionsValidator
│   │   ├── Persistence/
│   │   │   ├── AppDbContext.cs             ✎ + RefreshTokens
│   │   │   ├── Configurations/             ✚ RefreshTokenConfiguration; ✎ UserConfiguration (relationship)
│   │   │   └── Migrations/                 ✚ AddRefreshTokens
│   │   ├── Seeding/AccountSeeder.cs        ✎ uses IPasswordHasher; hash passed to ctor
│   │   └── DependencyInjection.cs          ✎ hasher, token service, JwtOptions
│   └── Store.API/
│       ├── Controllers/                    ✚ AuthController, CategoriesController, ProductsController
│       ├── Errors/                         ✚ ErrorResponse, ApiExceptionHandler, status-code/model-state writers
│       ├── Binding/                        ✚ ProductSort strict model binder
│       ├── Extensions/                     ✚ AddJwtAuthentication, AddSwagger, UseProductImages
│       └── Program.cs                      ✎ pipeline
└── tests/
    ├── Store.Domain.Tests/Entities/        ✚ RefreshTokenTests; ✎ UserTests
    ├── Store.Infrastructure.Tests/
    │   ├── Security/                       ✚ PasswordHasherTests, TokenServiceTests, JwtOptionsValidatorTests
    │   ├── Fixtures/Seeders.cs             ✎ pass PasswordHasher
    │   └── SchemaTests.cs                  ✎ RefreshTokens table, indexes, FK (AC 20)
    └── Store.API.Tests/                    ✚
        ├── Fixtures/                       StoreApiFactory, MutableTimeProvider, ApiClient helpers
        ├── ErrorShapeTests.cs              US5, SC-004, AC 10
        ├── StartupTests.cs                 missing/short Jwt keys, Swagger Dev-only
        ├── RegisterTests.cs, LoginTests.cs, MeTests.cs                 US3, AC 1–3, 12, 19
        ├── RefreshTokenTests.cs, LogoutTests.cs, RefreshRaceTests.cs   US4, AC 13–15
        ├── CategoriesTests.cs              AC 4
        ├── ProductListTests.cs, ProductSearchTests.cs, ProductPagingTests.cs   AC 5–7, 16–18
        ├── ProductDetailsTests.cs, InactiveProductTests.cs, FilterPanelTests.cs  AC 8, 9
        └── ImagesTests.cs                  AC 11
```

**Structure Decision**: extend the 001 four-project layout; Application gets feature folders per
the constitution; one new test project for HTTP-level tests. `Store.API` references
Application and Infrastructure as in 001.

## Key design points

- **Layering**: Application owns rules, DTOs, validators, error codes and exceptions; it knows no
  HTTP, JWT library or SQL Server. Infrastructure implements `IPasswordHasher`/`ITokenService`.
  API translates exceptions and framework errors to the shape.
- **Register flow**: validate → normalize → `AnyAsync(email)` → 409 | hash → `new User(..., Customer)`
  → save (catch `DbUpdateException` → re-check → 409) → issue tokens → 201
  ([R5](research.md#r5-duplicate-email-race)).
- **Login flow**: validate presence → find by lowercased email → `Verify` → same 401
  `invalidCredentials` for both failures (a dummy verify runs when the email is unknown, to keep
  timing similar) → issue tokens.
- **Token issue**: access token (JWT, 15 min) + refresh token (random, stored as hash, 7 days),
  one `RefreshToken` row per login/registration/refresh ([R2](research.md#r2-access-token-creation-and-validation),
  [R4](research.md#r4-refresh-tokens-generation-storage-rotation-reuse)).
- **Refresh**: hash → load tracked → unknown / expired / logged-out → 401; rotated → revoke all
  user's active tokens → 401; active → `MarkRotated` + new row in one transaction; concurrency
  conflict → reload → reuse handling → 401.
- **Logout**: hash → if active `Revoke(now)` → 204 always.
- **Catalog**: [R8–R10](research.md#r8-sorting-and-query-binding); details expose `maxQuantity`
  only; color+size filters match on the same in-stock variant (FR-035).
- **Errors**: [R6](research.md#r6-one-error-shape-for-every-failure) — exception handler, model
  state factory, JwtBearer challenge/forbidden events, status code pages.
- **Logging**: auth events log user id and outcome code only; refresh failures log the token row
  id when known, never the token or hash; exception handler logs type, message, trace id, route.
- **Startup order** (`Program.cs`): build → resolve `JwtOptions` (fail fast) → database
  initializer (001) → exception handler → status code pages → product images → Swagger (Dev) →
  authentication → authorization → controllers.
- **Coverage exclusions**: 001 list plus DTO-only records (via `ExcludeByFile` on
  `**/*Request.cs`, `**/*Response.cs`, constitution allows "DTOs without logic"); `Program.cs`
  and DI files stay excluded.

## Acceptance criteria → tests

| AC | Test class |
|---|---|
| 1 | RegisterTests |
| 2, 19 | LoginTests, MeTests (time advanced 15 min) |
| 3 | LoginTests |
| 4 | CategoriesTests |
| 5, 6, 7 | ProductListTests |
| 8 | ProductDetailsTests |
| 9 | InactiveProductTests |
| 10 | ErrorShapeTests |
| 11 | ImagesTests |
| 12 | LoginTests (both seeded accounts); PasswordHasherTests (001-format hash) |
| 13 | RefreshTokenTests, RefreshRaceTests |
| 14 | LogoutTests (both orders) |
| 15 | RefreshTokenTests (time advanced 7 days; DB scan for plain tokens) |
| 16, 17 | ProductSearchTests |
| 18 | ProductPagingTests (every sort, page size 5) |
| 20 | SchemaTests + full 001 suite |

## Complexity Tracking

No constitution violations requiring justification.

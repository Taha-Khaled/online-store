# Research: Backend Core — API Foundation, Authentication & Public Catalog

All technical unknowns are resolved below. Decisions marked *(developer)* were supplied with
`/speckit-plan` or answered during planning (2026-11-25); the rest are the agent's choices within
the constitution.

## R1. Password hasher abstraction

- **Decision** *(developer)*: `Store.Application/Common/Security/IPasswordHasher` with
  `string Hash(string password)` and `bool Verify(string hash, string password)`.
  `Store.Infrastructure/Security/PasswordHasher` wraps ASP.NET Core `PasswordHasher<object>` with a
  private static placeholder `object` as the "user" (the V3 algorithm ignores it). `Verify` treats
  `Success` and `SuccessRehashNeeded` as success.
- **Rationale**: the constitution requires ASP.NET Core `PasswordHasher`; the V3 format (PBKDF2,
  salt in the hash) does not depend on the user, so hashes made by the 001 seeder with
  `PasswordHasher<User>` verify unchanged (covered by a test that verifies a hash produced by
  `PasswordHasher<User>`). No `null!`, no empty-hash step.
- **Consequence**: `User` validates a non-empty hash in its constructor; `User.SetPasswordHash`
  existed only for the 001 workaround, has no other consumer, and is removed with its two tests
  (password reset is out of scope).
- **Alternatives**: `PasswordHasher<User>` behind the interface (needs a `User` instance — the
  problem being removed); a custom PBKDF2/BCrypt implementation (violates the constitution).

## R2. Access token creation and validation

- **Decision**: create JWTs with `JsonWebTokenHandler` (Microsoft.IdentityModel.JsonWebTokens, which
  ships with `Microsoft.AspNetCore.Authentication.JwtBearer`), HMAC-SHA256, claims `sub` (user id),
  `email`, `role`, plus `jti`. `IssuedAt`, `NotBefore` = now and `Expires` = now + 15 minutes, all
  from `TimeProvider`.
- **Validation**: `ValidateIssuer`, `ValidateAudience`, `ValidateIssuerSigningKey`,
  `ValidateLifetime`, `ClockSkew = TimeSpan.Zero`, `RoleClaimType = "role"`,
  `NameClaimType = "sub"`, `MapInboundClaims = false`. A custom `LifetimeValidator` compares
  `notBefore <= now < expires` using the injected `TimeProvider`, so a token is rejected the moment
  it is 15 minutes old and tests control "now" (the default validator reads the system clock).
- **Rationale**: one handler for creation and validation; FR-017/FR-019 and AC 19 are testable
  without waiting.
- **Alternatives**: `JwtSecurityTokenHandler` (legacy, separate package); relying on the default
  lifetime validator (system clock, cannot be tested deterministically).

## R3. JWT configuration and startup validation

- **Decision** *(developer)*: `JwtOptions` (`Key`, `Issuer`, `Audience`; constants
  `AccessTokenLifetime = 15 min`, `RefreshTokenLifetime = 7 days`) bound from section `Jwt`.
  A `JwtOptionsValidator : IValidateOptions<JwtOptions>` returns one failure per problem, each
  naming its key: "`Jwt:Key` is not configured", "`Jwt:Key` must be at least 32 bytes (UTF-8)",
  "`Jwt:Issuer` is not configured", "`Jwt:Audience` is not configured". Registered with
  `ValidateOnStart()`, and `Program.cs` reads `IOptions<JwtOptions>.Value` right after `Build()`,
  so the API stops **before** database initialization when a key is missing.
- **Rationale**: a clear message for every missing key at once; nothing touches the database
  with a broken configuration.
- **Alternatives**: data annotations on the options (no per-byte key length rule, worse
  messages); validating inside `AddJwtBearer` (fails only on the first authenticated request).

## R4. Refresh tokens: generation, storage, rotation, reuse

- **Generation** *(developer)*: 64 bytes from `RandomNumberGenerator`, encoded URL-safe base64
  (`Base64Url`, 86 characters). Stored value: SHA-256 of the token's UTF-8 bytes as uppercase hex
  (64 characters). A fast hash is correct because the token has 512 bits of entropy.
- **Rotation** *(developer, answered during planning)*: `RefreshToken.RevokedAt` is an EF Core
  **concurrency token**. Refresh loads the token tracked, calls `MarkRotated(now, newHash)`, adds
  the new token, and saves both in one `SaveChangesAsync` inside a transaction. EF emits
  `UPDATE ... WHERE Id = @id AND RevokedAt IS NULL`; if another request rotated it first, 0 rows
  change and EF throws `DbUpdateConcurrencyException`. The loser rolls back, reloads the token, and
  — because it is now rotated — runs reuse detection and returns 401.
  Same guarantee as a conditional `ExecuteUpdateAsync`, but the domain method enforces the rule.
- **Check order**: unknown → 401; **rotated** (checked before expiry, so a rotated token that has
  also expired still triggers reuse detection, FR-023) → reuse detection + 401; expired or
  logged-out → 401; otherwise active → rotate.
- **Reuse detection** (FR-023/FR-028): only a token with `ReplacedByTokenHash != null` triggers
  it. All of the user's active tokens are loaded and `Revoke(now)` is called on each, then saved.
  A token revoked by logout (no replacement) or expired just returns 401.
- **Logout** (FR-029): always 204; revokes the token only if `IsActive(now)`.
- **Errors**: every refresh failure returns 401 `invalidRefreshToken` (no hint about which case).
- **Rationale**: race-safe without locks; one code path; the database never holds a usable token
  (SC-005).
- **Alternatives**: conditional `ExecuteUpdateAsync` (bypasses `MarkRotated`); a rowversion
  column (extra column for the same effect); `SERIALIZABLE` transaction (heavier, deadlock-prone).
- **Not in scope**: cleanup of expired tokens (rows stay; acceptable for the POC).

## R5. Duplicate email race

- **Decision** *(developer, answered during planning)*: `AuthService.RegisterAsync` checks
  `AnyAsync(email)` first (409 `emailAlreadyExists`). If `SaveChangesAsync` still throws
  `DbUpdateException`, it re-queries the email with a fresh no-tracking query; if the email now
  exists → 409, otherwise the exception is rethrown (→ 500).
- **Rationale**: Store.Application stays free of SQL Server types; the unique index remains the
  safety net (constitution I). Covered by a test that inserts the email between the check and the
  save (two contexts).
- **Alternatives**: mapping `SqlException` 2601/2627 in Infrastructure to an Application exception
  (more code on the shared save path).

## R6. One error shape for every failure

- **Decision** *(developer)*:
  - `Store.Application/Common/Errors`: `ErrorCodes` constants and four exceptions —
    `ValidationException` (field → messages), `NotFoundException`, `ConflictException`,
    `UnauthorizedException` — each carrying a code and message. No HTTP types.
  - `Store.API`: `ErrorResponse(string Code, string Message, IDictionary<string,string[]>? Errors)`
    serialized camelCase, `errors` omitted when null.
  - `ApiExceptionHandler : IExceptionHandler` maps the four exceptions to 400/404/409/401 and
    anything else to 500 `internalError` with a generic message; the exception is logged with
    type, message, trace id and route — never the body.
  - `ApiBehaviorOptions.InvalidModelStateResponseFactory` → 400 `validationFailed` with field
    errors (e.g. `page=abc`, malformed JSON, missing body). Field names camelCase.
  - `MvcOptions.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true`, so model
    binding only reports binding errors and all business validation comes from FluentValidation in
    the services (constitution API Conventions, FR-005).
  - JwtBearer `OnChallenge` → `HandleResponse()` and write 401 `unauthorized`; `OnForbidden` →
    403 `forbidden`.
  - `UseStatusCodePages` writes the shape for any error status that has no body: 404 `notFound`,
    405 `methodNotAllowed`, 415 `unsupportedMediaType`, other → `requestFailed`. This also covers
    unknown routes and missing image files.
- **Rationale**: SC-004 (100% of errors in one shape).

## R7. Validation

- **Decision**: one validator per request record (`RegisterRequestValidator`,
  `LoginRequestValidator`, `RefreshRequestValidator`, `ProductListQueryValidator`,
  `ProductFiltersQueryValidator`). Services receive `IValidator<T>` and call
  `ValidateAsync`; failures become `ValidationException` (code `validationFailed`) with
  camelCase field names. Registered with `AddValidatorsFromAssembly` from
  `FluentValidation.DependencyInjectionExtensions` (approved).
- **Rules**: FR-013 for register (phone regex `^\+?[0-9]{1,19}$` so the total is ≤ 20);
  login: email and password not empty; refresh/logout: token not empty; product list: page ≥ 1,
  page size 1–48, search ≤ 100 characters, min/max price ≥ 0, min ≤ max, sort is a defined enum
  value.
- **Normalization** (spec Assumptions): email trimmed and lowercased, full name and phone trimmed,
  before validation and storage; search trimmed (empty → ignored).

## R8. Sorting and query binding

- **Decision**: `ProductSort` enum `Newest` (default), `PriceAsc`, `PriceDesc`, `NameAsc`, bound
  from `sort=` with a string-enum binder that accepts only these names (case-insensitive) and
  rejects numbers. An unknown value is a model binding error → 400 `validationFailed` with field
  `sort`. Each value maps to a fixed chain:
  - Newest: `CreatedAt desc, Id desc`
  - PriceAsc: `Price asc, Name asc, Id asc`
  - PriceDesc: `Price desc, Name asc, Id asc`
  - NameAsc: `Name asc, Id asc`
- **Rationale**: constitution III (enum options); FR-036 tiebreakers make paging stable (AC 18).
- **Implementation note**: the binder is a small `IModelBinder` for `ProductSort?`
  (`Enum.TryParse` with `ignoreCase` + `Enum.IsDefined` + reject numeric strings); the validator
  still checks `IsInEnum` as a second guard.

## R9. Case-insensitive, wildcard-safe search

- **Decision** *(developer)*: lowercase both sides in LINQ:
  `p.Name.ToLower().Contains(term)` (and description, category name, parent category name), where
  `term = search.Trim().ToLowerInvariant()`. EF Core (SQL Server provider) translates `Contains`
  with a parameter to `LOWER(col) LIKE @p ESCAPE N'\'`, escaping `%`, `_` and `[` in the parameter
  value. A test asserts the generated SQL (`ToQueryString()`) contains `ESCAPE`, and behavior tests
  search for `%`, `_` and `[` literally (no match on the seed data vs. "match everything").
- **Colors / sizes**: lowercased request values compared with `ToLower()` of `Color.Name` /
  `Size.Name` via `list.Contains(...)` (parameterized).
- **Rationale**: independent of the column collation; no raw SQL (constitution III).
- **Trade-off**: `LOWER(col)` prevents index seeks; irrelevant at 18 products.

## R10. Catalog queries

- **Decision**: `ProductService` and `CategoryService` use `IAppDbContext` with `AsNoTracking`
  and `Select` projections to DTOs.
  - Category filter: one query resolves the slug to its id and its children's ids; unknown →
    `NotFoundException(categoryNotFound)`.
  - Base filter: `IsActive`, featured, price range, category ids, search (R9).
  - Color/size (FR-035, same variant):
    `p.Variants.Any(v => v.Stock > 0 && (noColors || colors.Contains(v.Color.Name.ToLower())) && (noSizes || sizes.Contains(v.Size.Name.ToLower())))`, applied only when at
    least one of the two filters is given.
  - `CountAsync`, then the sort chain (R8), `Skip/Take`, project: main image =
    `Images.Where(i => i.IsMain).OrderBy(DisplayOrder).ThenBy(Id).Select(Url).FirstOrDefault()`;
    `inStock = Variants.Any(Stock > 0)`.
  - List colors: second query for the page's product ids — colors of each product's variants,
    ordered by the minimum `(DisplayOrder, Id)` of that color's images (colors without images last,
    by name). Merged in memory.
  - Details: one query by slug and `IsActive` with variants, colors, sizes and images projected;
    `maxQuantity = Math.Min(Stock, 5)` computed in the projection; stock itself never leaves the
    service. The 5 comes from a constant shared with the future cart (`CatalogLimits.MaxQuantityPerVariant`).
  - Filter panel: distinct colors (ordered by name) and sizes (ordered by `DisplayOrder`) of
    in-stock variants on active products in scope; `MinAsync`/`MaxAsync` of price over active
    products in scope (null when none).
- **Total pages**: `ceil(total / pageSize)`; 0 when total is 0.

## R11. Static images

- **Decision**: `UseStaticFiles` with a `PhysicalFileProvider` on `SeedOptions.ImagesPath` and
  `RequestPath = "/images/products"`, no directory browsing, registered before routing. Missing
  files fall through to the status-code-pages 404 (R6). The provider rejects paths outside the
  folder.
- **Rationale**: FR-007; no controller code; the folder is the one the 001 seeder already uses.

## R12. Swagger

- **Decision**: `Swashbuckle.AspNetCore` (approved), registered and mapped only when
  `IsDevelopment()`. One `Bearer` HTTP security scheme (`bearerFormat: JWT`) applied to
  `[Authorize]` actions. Version: the newest stable release supporting .NET 10 at implementation
  time, pinned exactly (the 10.x line uses Microsoft.OpenApi 2.x APIs for the security
  requirement).

## R13. API integration tests

- **Decision** *(developer)*: new project `Store.API.Tests` with
  `Microsoft.AspNetCore.Mvc.Testing` (approved), xUnit, Shouldly, coverlet. `StoreApiFactory :
  WebApplicationFactory<Program>, IAsyncLifetime`:
  - creates `StoreTests_<guid>` on `STORE_TEST_SQLSERVER` like 001 and drops it on dispose;
  - environment `Development`, so the real startup path migrates and seeds the test database;
  - settings via `UseSetting` (connection string, `Seed:*`, `Jwt:*` test values) because
    `Program.cs` reads the connection string before `Build()`; `UseSetting` values are visible
    there and override User Secrets;
  - replaces the `TimeProvider` singleton with a `MutableTimeProvider` (test-only class:
    `Now` + `Advance(TimeSpan)`), starting at the 001 seed time, so tokens expire on demand
    (no new package; `Microsoft.Extensions.TimeProvider.Testing` is not on the approved list).
  - the factory references `Program`; ASP.NET Core 10 generates a public `Program` declaration
    for top-level statements, so `public partial class Program;` is added only if the build shows
    it is still needed.
- **Isolation**: one factory (one seeded database) per test class (`IClassFixture`). Tests that
  change shared state — deactivating a product, reuse detection — use their own class or their own
  freshly registered user. Registration tests use unique emails.
- **Production-mode test**: a separate factory in `Production` against a database migrated by the
  fixture checks that Swagger returns 404 with the error shape.

## R14. Packages (all on the constitution's approved list)

| Project | Package | Version |
|---|---|---|
| Store.Application | `FluentValidation.DependencyInjectionExtensions` | latest 12.x stable |
| Store.Infrastructure | `Microsoft.AspNetCore.Authentication.JwtBearer` | 10.0.x matching the other ASP.NET Core / EF packages |
| Store.API | `Swashbuckle.AspNetCore` | R12 |
| Store.API.Tests | `Microsoft.AspNetCore.Mvc.Testing` | 10.0.x (same patch as JwtBearer) |

Exact versions are pinned when each package is added; no floating versions.

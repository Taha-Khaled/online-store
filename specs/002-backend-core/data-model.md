# Data Model: Backend Core

Changes on top of [001-database data-model](../001-database/data-model.md). One new table, one
changed entity; the catalog entities are read-only here.

## New: RefreshToken (`RefreshTokens`)

One login session on one device (spec Key Entities, FR-019–FR-024, FR-028, FR-029).

| Column | Type | Rules |
|---|---|---|
| `Id` | int, identity, PK | |
| `UserId` | int, FK → `Users.Id` | required; **cascade delete** |
| `TokenHash` | varchar(64) | required; SHA-256 of the token, uppercase hex; **unique index** |
| `CreatedAt` | datetime2 (UTC) | required |
| `ExpiresAt` | datetime2 (UTC) | required; `CreatedAt + 7 days` |
| `RevokedAt` | datetime2 (UTC), null | null = not revoked; **EF concurrency token** ([research R4](research.md#r4-refresh-tokens-generation-storage-rotation-reuse)) |
| `ReplacedByTokenHash` | varchar(64), null | hash of the token that replaced this one (set only by rotation) |

Indexes: unique `IX_RefreshTokens_TokenHash`; `IX_RefreshTokens_UserId`.
Relationship configured on the principal side in `UserConfiguration`
(`HasMany<RefreshToken>().WithOne(t => t.User).HasForeignKey(t => t.UserId)`), matching 001.

### Domain behavior (`Store.Domain/Entities/RefreshToken.cs`)

- Constructor `(userId, tokenHash, createdAt, expiresAt)`: hash not empty, `expiresAt > createdAt`
  (else `ArgumentException`).
- `bool IsActive(DateTime now)` → `RevokedAt is null && now < ExpiresAt`.
- `bool IsRotated` → `ReplacedByTokenHash is not null`.
- `void Revoke(DateTime now)` → sets `RevokedAt = now` if not already revoked; otherwise no change
  (keeps the first revocation time; safe for revoke-all).
- `void MarkRotated(DateTime now, string replacedByHash)` → requires `IsActive(now)` and a
  non-empty hash (else `InvalidOperationException` / `ArgumentException`); sets `RevokedAt = now`
  and `ReplacedByTokenHash`.

### States

```text
Active ──refresh──▶ Rotated (RevokedAt + ReplacedByTokenHash)   reuse ⇒ revoke all user tokens, 401
Active ──logout───▶ Revoked (RevokedAt only)                   reuse ⇒ 401 only
Active ──time─────▶ Expired (now ≥ ExpiresAt)                  use ⇒ 401 only
```

Migration: **`AddRefreshTokens`** (one named migration; Down drops the table).

## Changed: User

- Constructor validates `passwordHash` is not null/empty/whitespace (`ArgumentException`), plus
  the existing fields unchanged.
- `SetPasswordHash` is removed ([research R1](research.md#r1-password-hasher-abstraction)).
- Email is stored lowercase by `AuthService` (registration) and is already lowercase in the 001
  seed; the unique index on `Email` is unchanged.

## Read-only in this feature

`Category` (tree via `ParentCategoryId`), `Product` (`IsActive`, `IsFeatured`, `CreatedAt`,
`Price`), `ProductVariant` (`Stock` used only for `inStock` / `maxQuantity`), `ProductImage`
(`IsMain`, `DisplayOrder`, `ColorId`), `Color`, `Size` (`DisplayOrder`). No schema change.

## Application DTOs (records, Store.Application)

Full field lists are in [contracts/api.md](contracts/api.md). Highlights:

- `AuthResponse(string AccessToken, DateTime AccessTokenExpiresAt, string RefreshToken, DateTime RefreshTokenExpiresAt, UserResponse User)`
- `UserResponse(int Id, string FullName, string Email, string Role)`
- `PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount, int TotalPages)`
- `ProductListItem`, `ProductDetails`, `VariantResponse(… bool InStock, int MaxQuantity)` — no
  stock field anywhere.

# Quickstart: Backend Core (002-backend-core)

## Prerequisites

Everything from [001 quickstart](../001-database/quickstart.md) (SDK, local SQL Server
`localhost\MSSQLSERVER01`, `dotnet tool restore`, the three 001 secrets).

## Configuration (never committed)

Add three User Secrets for Store.API (from `backend/src/Store.API`):

| Secret | Purpose |
|---|---|
| `Jwt:Key` | HMAC signing key, at least 32 bytes |
| `Jwt:Issuer` | token issuer, e.g. `store-api` |
| `Jwt:Audience` | token audience, e.g. `store-web` |

Generate a strong random key (64 bytes, base64) and store it:

```powershell
$key = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(64))
dotnet user-secrets set "Jwt:Key" $key
dotnet user-secrets set "Jwt:Issuer" "store-api"
dotnet user-secrets set "Jwt:Audience" "store-web"
```

Production: environment variables `Jwt__Key`, `Jwt__Issuer`, `Jwt__Audience` in the hosting
panel.

## Database

Startup in Development applies the new `AddRefreshTokens` migration to the local `OnlineStore`
database. Existing data stays; the 001 demo accounts keep their passwords.

## Validate

1. **Startup check (US5)**: remove `Jwt:Key` → `dotnet run --project backend/src/Store.API` stops
   with a message naming `Jwt:Key`; set a 10-character key → message says it must be at least 32
   bytes. Restore the key.
2. **Swagger (US5)**: run in Development, open `/swagger`. Outside Development `/swagger` returns
   404 with the error shape.
3. **Auth (US3, US4)** from Swagger:
   - `POST /api/auth/login` as `customer@store.local` with the seeded password → tokens.
   - Authorize with the access token → `GET /api/auth/me` returns Demo Customer / Customer.
   - `POST /api/auth/refresh` → new pair; refresh again with the old token → 401
     `invalidRefreshToken`, and the new token stops working too (reuse detection).
   - Log in twice, log out the first refresh token → it returns 401, the second still refreshes.
   - Register a new customer → 201 with tokens; again with the email in upper case → 409.
4. **Catalog (US1, US2)**: compare responses with [contracts/api.md](contracts/api.md) and the
   spec's acceptance scenarios (18 products, featured 6, `shoes` 6, `shoes-sneakers` 2,
   `search=shoes&size=42` 5, `classic-cotton-t-shirt` details).
5. **Image**: open `/images/products/classic-cotton-t-shirt-black.jpg`.
6. **Database check (SC-005)**: `SELECT TokenHash FROM RefreshTokens` shows 64-character hex
   hashes only, never the token returned by the API.
7. **Full suite with coverage**:

   ```powershell
   $env:STORE_TEST_SQLSERVER = "Server=localhost\MSSQLSERVER01;Integrated Security=true;TrustServerCertificate=true"
   dotnet test backend --settings backend/coverage.runsettings --collect:"XPlat Code Coverage"
   ```

   API tests start the real app per test class on their own `StoreTests_<guid>` database with
   test-only JWT settings; nothing reads your User Secrets values for JWT.

## Production notes

- Applying `AddRefreshTokens` to the hosted database requires the developer's approval (as in
  001); until then the app starts, logs the pending migration count and skips seeding — but the
  auth endpoints need the table, so apply it before release.

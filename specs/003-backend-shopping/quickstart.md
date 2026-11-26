# Quickstart: Backend Shopping (003-backend-shopping)

## Prerequisites

Everything from the [001](../001-database/quickstart.md) and
[002](../002-backend-core/quickstart.md) quickstarts (SDK, local SQL Server
`localhost\MSSQLSERVER01`, the six User Secrets).

## Configuration

`Shop:ShippingFee` is in `appsettings.json` (default 50, not a secret). Production may override it
with the environment variable `Shop__ShippingFee`. A negative value stops the API at startup with
a message naming `Shop:ShippingFee`.

## Database

Startup in Development applies `AddOrderNumberSequence` and `AddOrderRowVersion` to the local
`OnlineStore` database. The first new order is `ORD-1001`.

## Validate

Log in as `customer@store.local` (seeded password) and use the access token for every call; see
[contracts/api.md](contracts/api.md) for bodies.

1. **Access (US5)**: any `/api/cart` or `/api/orders` call without a token → 401; with the
   `admin@store.local` token → 403 `forbidden`.
2. **Cart (US1)**: add Basic Ribbed Top White/S ×2 → subtotal 600, shipping 50, total 650,
   `canCheckout` true; add ×3 → quantity 5; add ×1 → 400 `maxQuantityExceeded`. Add Classic Cotton
   T-Shirt Black/M ×2 → 409 `insufficientStock`, `details.maxQuantity` 1.
3. **Merge (US4)**: on an empty cart merge [Black/M ×1, White/L ×1, variant 99999 ×1] → cart has
   Black/M; `adjustments` lists White/L `outOfStock` and 99999 `variantNotFound`. Repeat → same
   cart.
4. **Checkout (US2)**: with a cart of Basic Ribbed Top White/S ×2, `POST /api/orders` → 201,
   `ORD-1001` (or the next number), status Pending, total 650; `GET /api/cart` → empty. In SQL:
   that variant's `Stock` dropped by 2.
5. **Orders (US3)**: `GET /api/orders` → the new order first, then ORD-0008…ORD-0001. Cancel
   ORD-0008 → Cancelled; Black/L stock +2. Cancel it again, or ORD-0006 / ORD-0001 → 409
   `orderCannotBeCancelled`.
6. **Concurrency (US2-2/3/4)**: covered by the automated tests (parallel HTTP requests); not
   reproduced by hand.
7. **Full suite with coverage**:

   ```powershell
   $env:STORE_TEST_SQLSERVER = "Server=localhost\MSSQLSERVER01;Integrated Security=true;TrustServerCertificate=true"
   dotnet test backend --settings backend/coverage.runsettings --collect:"XPlat Code Coverage"
   ```

## Production notes

- Applying `AddOrderNumberSequence` and `AddOrderRowVersion` to the hosted database requires the
  developer's approval.
- Steps 4–5 change local data (orders, stock); re-run the 001 seeding on a fresh database to
  reset.

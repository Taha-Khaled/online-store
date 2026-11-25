# Research: Backend Shopping — Cart, Checkout & Customer Orders

Decisions marked *(developer)* were supplied with `/speckit-plan` (2026-11-25); the others are the
agent's choices within constitution 1.7.3 and the 002 structure.

## R1. Domain rules

- **Decision** *(developer)*:
  - `StoreLimits.MaxQuantityPerVariant = 5` moves to `Store.Domain` (replaces
    `Store.Application/Common/CatalogLimits`); `ProductService` (002) references the new constant.
  - `ProductVariant.Deduct(quantity)` — quantity ≥ 1, result never below 0;
    `ProductVariant.Restock(quantity)` — quantity ≥ 1.
  - `CartItem` constructor and `SetQuantity` enforce 1–5.
  - `Order.CreatePending(orderNumber, userId, shipping…, notes, items, shippingFee, now)` — the only
    way to create a new order; computes subtotal (Σ unit price × quantity) and total; status Pending;
    created = updated. `Order.CreateHistorical` stays for the seeder.
  - `Order.Cancel(now)` — Pending or Confirmed only; sets `UpdatedAt`.
- **Exception type**: one `DomainException(string message)` in `Store.Domain` (no framework
  dependency). *(developer)* Application services translate it to the operation's agreed code and
  never let it reach the handler as a 500: around `Deduct` → `cartChanged`; around `Order.Cancel`
  → `orderCannotBeCancelled`; around `CartItem` quantity changes → `maxQuantityExceeded`. Services
  also check the same rules before calling the domain so the normal path returns the detailed
  error (e.g. `cartChanged` with `details`); the translation is the backstop.
- **Rationale**: constitution I (rules in Domain, `Order` owns transitions and items) and IV.

## R2. Customer-only authorization

- **Decision** *(developer)*: policy `CustomerOnly` = `RequireRole("Customer")`, applied with
  `[Authorize(Policy = ...)]` on `CartController` and `OrdersController`. No token → JwtBearer
  challenge → 401 `unauthorized`; admin token → JwtBearer `OnForbidden` → 403 `forbidden` (002
  writer, research R6). Policy name constant lives in `Store.API`.

## R3. Shipping fee configuration

- **Decision** *(developer)*: `ShopOptions { ShippingFee = 50 }` bound from section `Shop`,
  `"Shop": { "ShippingFee": 50 }` in `appsettings.json` (not a secret). `ShopOptionsValidator`
  (`IValidateOptions`) rejects a negative fee with a message naming `Shop:ShippingFee`;
  `ValidateOnStart` and read right after `Build()` like `JwtOptions`. The class lives in
  `Store.Application/Common` because services use it.

## R4. Order numbers

- **Decision** *(developer)*: SQL Server sequence `dbo.OrderNumberSequence` (bigint, start 1001,
  increment 1) declared with `modelBuilder.HasSequence<long>(...)` and created by the named
  migration **`AddOrderNumberSequence`**. Format `ORD-{value}` → `ORD-1001`, `ORD-1002`, …
- **Where it is read**: `Database.SqlQuery<T>` is in `Microsoft.EntityFrameworkCore.Relational`,
  which Store.Application does not reference (only the base package is approved there). So
  Application declares `IOrderNumberGenerator.NextAsync(CancellationToken)` and Infrastructure
  implements it with
  `Database.SqlQuery<long>($"SELECT NEXT VALUE FOR dbo.OrderNumberSequence AS [Value]").ToListAsync()`
  then `Single()`. `ToListAsync` without other operators keeps EF from wrapping the statement in a
  subquery (SQL Server forbids `NEXT VALUE FOR` inside derived tables); same pattern as the 001
  procedure call. Interpolated `SqlQuery` without user values — allowed by constitution III.
- **Gaps**: a failed or retried checkout consumes a number; acceptable *(developer)*. Values are
  increasing, never reused, and start above the seeded ORD-0001…ORD-0008.
- **Alternatives**: `MAX(OrderNumber)+1` (race-prone); identity-derived number after insert (two
  saves per order).

## R5. Checkout algorithm (critical area)

- **Decision** *(developer)* — `CheckoutService.CheckoutAsync(userId, request)`:
  1. Validate the request (FluentValidation; phone rule shared with registration).
  2. Up to **3 attempts**, each: `ChangeTracker.Clear()`, begin a transaction, then
     - load the customer's cart items **tracked** with variant → product, color and size;
     - empty → 400 `cartEmpty`;
     - compute each item's status (R7); any non-`available` → 409 `cartChanged` with `details`
       (R9), roll back, nothing saved;
     - `variant.Deduct(quantity)` per item (domain refuses < 0);
     - `orderNumber = await orderNumbers.NextAsync()`;
     - `Order.CreatePending(...)` with snapshots (product name, color name, size name, current
       price, quantity) and the configured fee; add it;
     - remove the cart items;
     - `SaveChangesAsync` + commit.
     - `DbUpdateConcurrencyException` → roll back and start the next attempt.
  3. Three conflicts → 409 `checkoutConflict`.
- **Why it never oversells**: every variant UPDATE is `WHERE Id = @id AND RowVersion = @original`
  (rowversion from 001). A competing commit changes the rowversion, so a stale deduction updates 0
  rows → `DbUpdateConcurrencyException` → nothing of that attempt is saved (transaction) → the next
  attempt re-reads the real stock. The `Stock >= 0` check constraint is the safety net.
- **Same customer twice**: both attempts delete the same cart rows and update the same variants;
  the loser's DELETE/UPDATE affects 0 rows → conflict → its retry finds an empty cart → 400
  `cartEmpty`. At most one order.
- **Exactly N orders for N units** (AC 7): a request gives up with `checkoutConflict` only after 3
  conflicts, and each conflict is caused by another request's successful checkout of that variant
  (it re-reads after each). So a give-up implies ≥ 3 successes; otherwise every loser ends with
  `cartChanged` once stock is 0. With stock 3 and 10 buyers: exactly 3 orders, 7 × 409
  (`cartChanged` or `checkoutConflict`), stock 0.
- **Infrastructure needs**: `IAppDbContext` gains `ChangeTracker ChangeTracker { get; }`
  (base EF package) so services can clear tracking between attempts. Transactions use
  `Database.BeginTransactionAsync` (base package).
- **Alternatives**: `UPDATE … SET Stock = Stock - @q WHERE Stock >= @q` via `ExecuteUpdate`
  (bypasses `Deduct`; the developer chose the domain method + rowversion); serializable
  isolation (deadlock-prone).

## R6. Cancel algorithm

- **Decision** *(developer)* — `CustomerOrderService.CancelAsync(userId, orderId)`: same retry
  shell (3 attempts, `ChangeTracker.Clear()`, transaction). Load the order **tracked** with items
  and their variants; not found or other customer → 404 `orderNotFound`; not Pending → 409
  `orderCannotBeCancelled`; `order.Cancel(now)`; `variant.Restock(quantity)` per item; save;
  commit. Three conflicts → 409 `orderConflict`.
- **Concurrency token on Order** *(developer, 2026-11-26)*: `Orders.RowVersion` (rowversion,
  `IsRowVersion`) added by the named migration **`AddOrderRowVersion`**. Reason: in 004 an admin
  confirms an order without touching stock, so variant rowversions alone would not stop a customer
  cancel based on a stale Pending read from overwriting Confirmed. With the order token, the
  cancel's UPDATE is `WHERE Id = @id AND RowVersion = @original`; a status change in between makes
  it affect 0 rows → `DbUpdateConcurrencyException` → the retry re-reads the status (now
  Confirmed) → 409 `orderCannotBeCancelled`, no stock change. Two simultaneous customer cancels
  resolve the same way (the second sees Cancelled). Checkout inserts orders, so it is unaffected.
- **Race test** *(developer)*: `CancelOrderRaceTests` (Store.Infrastructure.Tests) builds
  `CustomerOrderService` directly on a seeded test database with a `SaveChangesInterceptor` that,
  on the first save only, sets the order's status to Confirmed through a second context; asserts
  409 `orderCannotBeCancelled`, status Confirmed, and unchanged variant stock.
- **Shared retry helper**: one small `ConcurrencyRetry.RunAsync(db, attempt, onExhausted)` in
  `Store.Application/Common` used by checkout and cancel (no duplicated loop).

## R7. Cart availability and projection

- **Decision**: `CartService` reads the cart with one `AsNoTracking` projection per request: for
  each line — ids, product name/slug, color, size, `Price`, `IsActive`, quantity, `maxQuantity =
  Stock < 5 ? Stock : 5`, `isActive`, and a status computed in SQL:
  `!IsActive → unavailable; Stock == 0 → outOfStock; Stock < Quantity → quantityReduced; else
  available`. Main image: the product's `IsMain` image with `ColorId = variant.ColorId`, lowest
  `DisplayOrder`, then `Id`; null if none. Exact stock never leaves the query (002 rule).
- **Totals** (spec Assumptions): subtotal = Σ line totals of `available` items; shipping fee =
  configured fee if subtotal > 0 else 0; total = subtotal + fee; `canCheckout` = items > 0 and all
  `available`. Lines ordered by `AddedAt`, then `Id`.
- **Status as API string**: enum `CartItemStatus` serialized camelCase (`available`,
  `quantityReduced`, `outOfStock`, `unavailable`) via `JsonStringEnumConverter(CamelCase)`
  registered for the API.

## R8. Cart writes and the add race

- **Add**: validate (variant id > 0, quantity 1–5) → load variant with product (no tracking) →
  unknown/inactive → 404 `variantNotFound` → existing line (tracked)? new quantity = existing +
  requested → > 5 → 400 `maxQuantityExceeded` → > stock → 409 `insufficientStock` (`details`:
  `{ variantId, maxQuantity }`) → new line and cart already has 30 lines → 409 `cartFull` → save.
- **Race** *(developer)*: two adds of the same new variant can both insert; the unique index
  `(UserId, ProductVariantId)` rejects one → catch `DbUpdateException`, `ChangeTracker.Clear()`,
  reload; if a line for that variant now exists, apply the add once more as an update (same
  checks); otherwise rethrow (→ 500). Same idea as the 002 duplicate-email race.
- **Update** (`PUT`): line must belong to the customer (else 404 `cartItemNotFound`); variant's
  product inactive → 404 `variantNotFound`; quantity 1–5 (400) and ≤ stock (409
  `insufficientStock`).
- **Remove / clear**: delete own line (404 if not own); clear = `ExecuteDeleteAsync` on the
  customer's lines → 204.

## R9. Merge

- **Decision** *(developer + spec)*: validate (≤ 30 entries; each variant id > 0, quantity 1–5).
  Collapse duplicate variant ids to the highest quantity. Load the variants (with product) and the
  customer's existing lines in two queries. Per entry, in list order:
  - unknown or inactive → skipped `variantNotFound`;
  - stock 0 → skipped `outOfStock`;
  - target = max(existing, guest); capped = min(target, 5, stock);
  - no existing line and the cart already has 30 lines → skipped `cartFull`;
  - capped < target → reported `quantityReduced` (with `requestedQuantity`, `quantity`);
  - set/insert the line only if the value changes.
  One `SaveChangesAsync`. Response = cart + `adjustments: [{ variantId, reason, requestedQuantity,
  quantity }]`. Idempotent because the rule is max, not sum. The add race (R8) applies here too
  (retry once on unique violation).

## R10. Error details

- **Decision** *(developer)*: the shared error shape gains an optional `details` (omitted when
  null, like `errors`). `AppException` gets `object? Details`; `ConflictException(code, message,
  details)` carries it; `ApiExceptionHandler` writes it. Shapes:
  - `insufficientStock`: `{ "variantId": 12, "maxQuantity": 1 }`
  - `cartChanged`: `{ "items": [{ "itemId", "variantId", "status", "maxQuantity" }] }`
- 002 errors are unchanged (no `details`).

## R11. Error codes added

`cartEmpty` (400), `maxQuantityExceeded` (400), `cartItemNotFound` (404), `variantNotFound` (404),
`orderNotFound` (404), `insufficientStock` (409), `cartFull` (409), `cartChanged` (409),
`checkoutConflict` (409), `orderCannotBeCancelled` (409), `orderConflict` (409). `cartEmpty` and
`maxQuantityExceeded` are 400 with the error shape but are thrown as a new `BadRequestException`
(code + message, no field errors) because they are business rules, not field validation.

## R12. Customer orders

- **List**: `AsNoTracking`, own orders only, `CreatedAt desc, Id desc`, paged (page ≥ 1, size
  1–50, default 10), projected to `{ id, orderNumber, status, itemCount, total, createdAt }`.
- **Details**: own order or 404; snapshot fields only (no join to current product data); line total
  = unit price × quantity; `canCancel` = Pending.
- Status serialized as its name (`Pending`, `Confirmed`, …), as stored.

## R13. Tests

- **Domain** *(developer)*: `ProductVariantTests` (Deduct/Restock), `CartItemTests`, `OrderTests`
  (CreatePending totals and status, Cancel transitions; 001 `CreateHistorical` tests unchanged).
- **API**: one `StoreApiFactory` (own database) per test class; classes that change stock, cancel
  seeded orders or run in parallel are separate classes. New customers are registered per test
  (`AuthClient.RegisterNewCustomerAsync`) so carts never collide.
- **Concurrency** (AC 6, 7, 8): real parallel HTTP requests (`Task.WhenAll`); assertions on order
  count, response codes and final stock read from the database; each test run 5+ times before its
  commit (as for the 002 refresh race).
- **Infrastructure**: `OrderNumberGeneratorTests` (starts at 1001, increasing, unique under
  parallel calls), `CancelOrderRaceTests` (R6), and `SchemaTests` updated for the sequence and
  `Orders.RowVersion`.
- **Coverage**: ≥ 80% lines and branches on changed files, async bodies counted (002 settings).

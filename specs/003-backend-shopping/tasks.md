---

description: "Task list for 003-backend-shopping: Cart, Checkout & Customer Orders"
---

# Tasks: Backend Shopping — Cart, Checkout & Customer Orders

**Input**: Design documents from `/specs/003-backend-shopping/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md),
[data-model.md](data-model.md), [contracts/api.md](contracts/api.md), [quickstart.md](quickstart.md)

**Tests**: Required (developer's rules, constitution Quality Gates: checkout concurrency and order
transitions, 80% coverage with async bodies counted). Within each story the implementation task
comes first and its test task follows, so every commit builds.

**Work items**: each task ID is the work item, written `#003-<task>`
(e.g. `feat #003-T022 add checkout`). Exactly one task per commit; a task with no code change
produces no commit. The PR into `develop` includes `Closes #7`. `tasks.md` `[X]` marks are
committed once at the end (`docs #003 ...`).

**Critical areas** (constitution "AI-Assisted Development"): stock changes (T004, T022, T025),
checkout concurrency (T022, T024) and order transitions (T008, T025, T028) need an explanation of
the implementation in the report, alongside their tests.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: User story from spec.md (US1–US5)
- Paths are relative to the repository root; backend code lives under `backend/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Shared constants and configuration

- [ ] T001 Move the per-variant limit to the domain: create `backend/src/Store.Domain/StoreLimits.cs` (`MaxQuantityPerVariant = 5`, `MaxCartLines = 30`), delete `backend/src/Store.Application/Common/CatalogLimits.cs`, and point `backend/src/Store.Application/Products/ProductService.cs` at `StoreLimits` (research R1); 002 tests stay green (refactor, no behavior change)
- [ ] T002 Add `ShopOptions` (`ShippingFee`, default 50, section `Shop`) and `ShopOptionsValidator` (`IValidateOptions`, fee ≥ 0, message names `Shop:ShippingFee`) in `backend/src/Store.Application/Common/ShopOptions.cs`; bind with `ValidateOnStart` in `backend/src/Store.Infrastructure/DependencyInjection.cs`; add `"Shop": { "ShippingFee": 50 }` to `backend/src/Store.API/appsettings.json`; read `IOptions<ShopOptions>.Value` right after `Build()` in `backend/src/Store.API/Program.cs` next to the JWT check (research R3)
- [ ] T003 Write `backend/tests/Store.Infrastructure.Tests/Shop/ShopOptionsValidatorTests.cs` (0 and 50 pass; −1 fails naming `Shop:ShippingFee`) and add a case to `backend/tests/Store.API.Tests/StartupTests.cs`: `Shop:ShippingFee = -1` stops startup naming the key before the database is created

**Checkpoint**: build green; all 001/002 tests pass

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Domain rules, schema, order numbers, error plumbing and access policy used by every story

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [ ] T004 Create `backend/src/Store.Domain/DomainException.cs` and add `Deduct(int quantity)` / `Restock(int quantity)` to `backend/src/Store.Domain/Entities/ProductVariant.cs` per data-model.md "Changed: ProductVariant" (data-model.md ProductVariant.Stock: quantity ≥ 1, never below 0)
- [ ] T005 Write `backend/tests/Store.Domain.Tests/Entities/ProductVariantTests.cs`: deduct to exactly 0; deduct more than stock throws and leaves stock unchanged; quantity 0/negative throws for both methods; restock adds
- [ ] T006 Enforce data-model.md CartItem.Quantity (1–`StoreLimits.MaxQuantityPerVariant`) in the constructor and a new `SetQuantity(int)` in `backend/src/Store.Domain/Entities/CartItem.cs` (`DomainException` outside the range)
- [ ] T007 Write `backend/tests/Store.Domain.Tests/Entities/CartItemTests.cs`: 1 and 5 accepted; 0 and 6 rejected in constructor and `SetQuantity`; `SetQuantity` changes the value
- [ ] T008 Add to `backend/src/Store.Domain/Entities/Order.cs`: `CreatePending(...)` and `Cancel(now)` per data-model.md "Changed: Order" (data-model.md Order.Subtotal, Order.Total, Order.Status, Order.CreatedAt, Order.UpdatedAt; OrderItem.UnitPrice, OrderItem.Quantity) and the property data-model.md Order.RowVersion (`byte[]`, private setter); `CreateHistorical` unchanged
- [ ] T009 Extend `backend/tests/Store.Domain.Tests/Entities/OrderTests.cs`: `CreatePending` → Pending, subtotal/total from items and fee, created = updated, items owned; rejects empty number, no items, negative fee; `Cancel` from Pending and Confirmed → Cancelled with new `UpdatedAt`; from Shipped, Delivered, Cancelled → `DomainException`, status unchanged; existing `CreateHistorical` tests untouched
- [ ] T010 Persist the order concurrency token: `RowVersion` `IsRowVersion()` in `backend/src/Store.Infrastructure/Persistence/Configurations/OrderConfiguration.cs`; migration **`AddOrderRowVersion`** in `backend/src/Store.Infrastructure/Persistence/Migrations/` (`dotnet ef migrations add AddOrderRowVersion --project src/Store.Infrastructure --startup-project src/Store.API --output-dir Persistence/Migrations`, Development environment); review; apply to the local SQL Server (data-model.md Order.RowVersion; research R6)
- [ ] T011 Order numbers (research R4): `HasSequence<long>("OrderNumberSequence").StartsAt(1001)` in `backend/src/Store.Infrastructure/Persistence/AppDbContext.cs`; migration **`AddOrderNumberSequence`**; `backend/src/Store.Application/Common/IOrderNumberGenerator.cs`; `backend/src/Store.Infrastructure/Orders/OrderNumberGenerator.cs` (injects `IAppDbContext`, constitution II; `db.Database.SqlQuery<long>($"SELECT NEXT VALUE FOR dbo.OrderNumberSequence AS [Value]").ToListAsync()` → `ORD-{value}`); register scoped in `AddInfrastructure`; apply to the local SQL Server
- [ ] T012 Write `backend/tests/Store.Infrastructure.Tests/Orders/OrderNumberGeneratorTests.cs` (first value `ORD-1001`, increasing, 20 parallel calls on separate contexts give 20 distinct numbers, never one of ORD-0001…0008) and update `backend/tests/Store.Infrastructure.Tests/SchemaTests.cs` (`Orders.RowVersion` is `timestamp`/not null; sequence exists with start 1001; full 001/002 suite green)
- [ ] T013 Error and retry plumbing (research R5, R10, R11): new codes in `backend/src/Store.Application/Common/Errors/ErrorCodes.cs`; `AppException.Details` (`object?`); `ConflictException` overload with details; `BadRequestException` (400, code + message); `ChangeTracker ChangeTracker { get; }` on `backend/src/Store.Application/Common/IAppDbContext.cs`; `backend/src/Store.Application/Common/ConcurrencyRetry.cs` (runs an attempt delegate up to 3 times; before each: `ChangeTracker.Clear()`; each attempt runs inside a transaction the helper begins and commits, rolled back on any exception — services never open their own; `DbUpdateConcurrencyException` → next attempt; exhausted → caller-supplied exception); `ErrorResponse.Details` (omitted when null) and `BadRequestException` → 400 in `backend/src/Store.API/Errors/ApiExceptionHandler.cs`
- [ ] T014 Write tests for T013: `backend/tests/Store.API.Tests/ErrorShapeTests.cs` (+ test controller actions: `BadRequestException` → 400 with code; `ConflictException` with details → `details` in JSON; 002 errors still have no `details`) and `backend/tests/Store.Infrastructure.Tests/Common/ConcurrencyRetryTests.cs` (success first try; conflict then success → 2 attempts and tracker cleared; 3 conflicts → the supplied exception; other exceptions propagate immediately)
- [ ] T015 Customer access and API plumbing: `backend/src/Store.API/Authorization/Policies.cs` (`CustomerOnly` = role Customer) registered in `Program.cs` (research R2); JSON names: `CartItemStatus` and `MergeAdjustmentReason` serialize as camelCase strings (`[JsonConverter(typeof(JsonStringEnumConverter<T>))]` with `[JsonStringEnumMemberName("...")]` on each value), and order DTOs carry `Status` as the `OrderStatus` name in a `string` (`"Pending"`), never a number; `backend/tests/Store.API.Tests/Fixtures/ShoppingClient.cs` (variant id by SKU from the test database, cart/order request helpers, `LoginCustomerAsync`, `LoginAdminAsync`, `NewCustomerAsync`)

**Checkpoint**: domain rules tested; schema migrated; error `details` and retry shell proven

---

## Phase 3: User Story 1 - Customer keeps a cart on the server (Priority: P1) 🎯 MVP

**Goal**: read, add, update, remove and clear cart lines with live price, availability and totals

**Independent Test**: as a fresh customer, add/update/remove items on the seeded store and read
the cart (spec US1)

### Implementation for User Story 1

- [ ] T016 [US1] Create `backend/src/Store.Application/Cart/`: `CartItemStatus.cs`, `CartResponse.cs` / `CartItemResponse.cs` (fields per [contracts/api.md](contracts/api.md)), `CartReader.cs` (`AsNoTracking` projection: status and `maxQuantity = Stock < 5 ? Stock : 5` computed in SQL, main image for the variant's color, lines by `AddedAt` then `Id`; subtotal of `available` lines, fee from `ShopOptions` only when subtotal > 0, `canCheckout` — research R7); register in `AddApplication`
- [ ] T017 [US1] Create `backend/src/Store.Application/Cart/CartService.cs` with `AddItemAsync`, `UpdateItemAsync`, `RemoveItemAsync`, `ClearAsync` and validators `AddCartItemRequestValidator.cs` / `UpdateCartItemRequestValidator.cs` (variant id > 0, quantity 1–5) per research R8: ownership → 404 `cartItemNotFound`; unknown/inactive → 404 `variantNotFound`; sum > 5 → 400 `maxQuantityExceeded`; > stock → 409 `insufficientStock` with `details { variantId, maxQuantity }`; 31st line → 409 `cartFull`; quantity changes through `CartItem`/`SetQuantity` (`DomainException` → `maxQuantityExceeded`); unique-index race → clear tracker, reload, apply once as update (cart lines have no concurrency token: parallel adds to an existing line may lose an increment — accepted, spec Clarifications); clear with `ExecuteDeleteAsync`; returns `CartReader` result; logs user id, variant id, outcome only
- [ ] T018 [US1] Create `backend/src/Store.API/Controllers/CartController.cs` (`[Authorize(Policy = CustomerOnly)]`): `GET /api/cart`, `POST /api/cart/items`, `PUT /api/cart/items/{itemId}`, `DELETE /api/cart/items/{itemId}`, `DELETE /api/cart` (204); user id from `sub`

### Tests for User Story 1

- [ ] T019 [US1] Write `backend/tests/Store.API.Tests/CartTests.cs` (fresh customer per test): AC 2 (White/S ×2 → 600/50/650, `canCheckout`; +3 → 5; +1 → 400 `maxQuantityExceeded`, cart unchanged); AC 3 (Black/M ×2 → 409 `insufficientStock` `details.maxQuantity` 1; ×1 ok; White/L → `maxQuantity` 0); empty cart → 0/0/0, `canCheckout` false; update to 3, remove, clear (204); another customer's item id → 404 `cartItemNotFound`; quantity 0/6 → 400; unknown variant → 404 `variantNotFound`; image URL for the color; response has no `stock` field; after add, update, remove and clear the variant's stock in the database is unchanged (FR-011)
- [ ] T020 [P] [US1] Write `backend/tests/Store.API.Tests/CartLimitsTests.cs`: 30 different variants accepted, 31st → 409 `cartFull`; 10 parallel adds of the same new variant ×1 → no 500 (every response 200, 400 `maxQuantityExceeded` or 409), exactly one line for that variant, quantity 1–5 (an increment may be lost: accepted, spec Clarifications)
- [ ] T021 [P] [US1] Write `backend/tests/Store.API.Tests/CartAvailabilityTests.cs` (own database): after adding lines, change data directly in the database — product deactivated → `unavailable`; stock 0 → `outOfStock`; stock below quantity → `quantityReduced` with `maxQuantity`; price changed → new unit price and line total; `subtotal` counts only `available` lines; `canCheckout` false; updating the inactive product's line → 404 `variantNotFound`

**Checkpoint**: US1 passes — server cart usable

---

## Phase 4: User Story 2 - Customer checks out without overselling (Priority: P1)

**Goal**: cash-on-delivery checkout in one transaction with retry on conflicts

**Independent Test**: build carts for one or several customers, check out sequentially and in
parallel; compare orders, stock and carts (spec US2)

### Implementation for User Story 2

- [ ] T022 [US2] Create `backend/src/Store.Application/Orders/`: `PlaceOrderRequest.cs` + `PlaceOrderRequestValidator.cs` (FR-014; strings trimmed; phone rule shared with registration: move the private `IsPhoneNumber` from `backend/src/Store.Application/Auth/RegisterRequestValidator.cs` to `backend/src/Store.Application/Common/Validation/PhoneNumbers.cs` (`IsValid`) and use it from both validators; 002 register tests stay green), `OrderDetailsResponse.cs` / `OrderItemResponse.cs` (contracts/api.md), `OrderMapping.cs` (order → details, `canCancel` = Pending), and `CheckoutService.cs` per research R5 inside `ConcurrencyRetry`: load cart tracked with variant/product/color/size; empty → 400 `cartEmpty`; any non-available → 409 `cartChanged` with `details.items`; `Deduct` (`DomainException` → `cartChanged`); `IOrderNumberGenerator`; `Order.CreatePending` with snapshots (data-model.md OrderItem.ProductName, OrderItem.ColorName, OrderItem.SizeName, OrderItem.UnitPrice, OrderItem.Quantity from current catalog data) and `ShopOptions.ShippingFee`; remove cart lines; save (the retry helper commits); exhausted → 409 `checkoutConflict`; logs user id, order number, line count, attempt, outcome; register in `AddApplication`; add `POST /api/orders` (201, Location `/api/orders/{id}`) in new `backend/src/Store.API/Controllers/OrdersController.cs` (`CustomerOnly`)

### Tests for User Story 2

- [ ] T023 [US2] Write `backend/tests/Store.API.Tests/CheckoutTests.cs` (own database): AC 5 (White/S ×2 → 201, Pending, `ORD-1001`+, total 650, snapshot name/color/size/price 300/qty 2, stock −2 in the database, cart empty, Location header); AC 12 (empty cart → 400 `cartEmpty`; missing city / phone with letters → 400 field errors, validated before the cart); AC 9 checkout part (deactivated product in cart → 409 `cartChanged` listing it, no order, no stock change, cart unchanged); `quantityReduced` line → 409 `cartChanged` with `maxQuantity`; notes stored; order number increasing across two checkouts
- [ ] T024 [US2] Write `backend/tests/Store.API.Tests/CheckoutConcurrencyTests.cs` (each test its own factory/database): AC 6 (2 customers, Black/M stock 1, parallel → 1 order, other 409 `cartChanged`, stock 0); AC 7 (10 customers, Everyday White Sneakers White/43 stock 3, parallel → exactly 3 orders, 7 × 409, stock 0); AC 8 (same customer, 2 parallel checkouts → exactly 1 order, other 400 `cartEmpty` or 409); assertions read orders and stock from the database; run the class 5+ times before committing (SC-002)

**Checkpoint**: US1 + US2 pass — customers can buy, never oversold

---

## Phase 5: User Story 3 - Customer sees and cancels their own orders (Priority: P2)

**Goal**: own order list, details and Pending-only cancel with stock return

**Independent Test**: as the demo customer, list/open orders, cancel Pending and non-Pending
orders and check stock (spec US3)

### Implementation for User Story 3

- [ ] T025 [US3] Create `backend/src/Store.Application/Orders/CustomerOrderService.cs` (+ `OrderListQuery.cs` / validator: page ≥ 1, page size 1–50, default 10; `OrderSummaryResponse.cs`): `ListAsync` (own orders, `AsNoTracking`, `CreatedAt` desc then `Id` desc, item count, paged — research R12), `GetAsync` (own or 404 `orderNotFound`, snapshot fields only), `CancelAsync` per research R6 inside `ConcurrencyRetry` (tracked order + items + variants; not own → 404; not Pending → 409 `orderCannotBeCancelled`; `Order.Cancel` (`DomainException` → `orderCannotBeCancelled`); `Restock` each line; save (the retry helper commits); exhausted → 409 `orderConflict`); logs user id, order id, outcome; register in `AddApplication`; add `GET /api/orders`, `GET /api/orders/{id}`, `POST /api/orders/{id}/cancel` to `OrdersController`

### Tests for User Story 3

- [ ] T026 [US3] Write `backend/tests/Store.API.Tests/CustomerOrdersTests.cs`: AC 10 (demo customer: 8 orders, ORD-0008 first, then ORD-0007…; details of ORD-0001 with snapshot items, subtotal/fee/total, `canCancel` false; ORD-0008 `canCancel` true); a new customer's orders list is empty; another customer's order id → 404 `orderNotFound` for details and cancel; page beyond last → empty with totals; page size 51 → 400
- [ ] T027 [US3] Write `backend/tests/Store.API.Tests/CancelOrderTests.cs` (own database): AC 11 (cancel ORD-0008 → Cancelled, `updatedAt` changed, Black/L stock +2 in the database; cancel again → 409 `orderCannotBeCancelled`, stock unchanged; ORD-0006 (Confirmed) and ORD-0001 (Delivered) → 409, stock unchanged); cancelling an order placed by checkout returns its stock
- [ ] T028 [US3] Write `backend/tests/Store.Infrastructure.Tests/Orders/CancelOrderRaceTests.cs` (research R6): build `CustomerOrderService` directly on a migrated + seeded test database with a `SaveChangesInterceptor` that, before the first save only, sets ORD-0008's status to Confirmed through a second context → 409 `orderCannotBeCancelled`, status Confirmed, Black/L stock unchanged; a second test where the interceptor, before every save, updates ORD-0008's `Notes` (status stays Pending, row version changes) through a second context → 409 `orderConflict` after 3 attempts, status still Pending, stock unchanged

**Checkpoint**: US3 passes

---

## Phase 6: User Story 4 - Guest cart is merged after login (Priority: P2)

**Goal**: idempotent guest cart merge with a report of skipped/reduced entries

**Independent Test**: merge lists with valid, out-of-stock and unknown variants into empty and
non-empty carts; merge twice (spec US4)

### Implementation for User Story 4

- [ ] T029 [US4] Create `backend/src/Store.Application/Cart/CartMergeService.cs`, `MergeCartRequest.cs` + validator (≤ 30 entries, variant id > 0, quantity 1–5), `MergeAdjustmentReason.cs`, `MergeCartResponse.cs` per research R9 (collapse duplicates to the max; skip `variantNotFound` / `outOfStock` / `cartFull`; `quantityReduced` when capped; max-not-sum; one save; unique-index race retried once like T017; returns `CartReader` result + adjustments); register in `AddApplication`; add `POST /api/cart/merge` to `CartController`

### Tests for User Story 4

- [ ] T030 [US4] Write `backend/tests/Store.API.Tests/CartMergeTests.cs`: AC 4 ([Black/M ×1, White/L ×1, 99999 ×1] → only Black/M; adjustments White/L `outOfStock`, 99999 `variantNotFound`; same merge again → identical cart, Black/M still ×1); existing ×3 + guest ×2 → 3; existing ×1 + guest ×4 → 4; guest ×5 on Black/M (stock 1) → 1 with `quantityReduced`; duplicates in the list → one line with the higher quantity; cart with 30 lines → new entries `cartFull`; 31 entries or quantity 6 → 400

**Checkpoint**: US4 passes

---

## Phase 7: User Story 5 - Only customers shop (Priority: P3)

**Goal**: every shopping endpoint rejects anonymous callers and admins

**Independent Test**: call all 9 endpoints without a token and with the admin token (spec US5)

### Tests for User Story 5

- [ ] T031 [US5] Write `backend/tests/Store.API.Tests/ShoppingAccessTests.cs`: AC 1 — theory over all 9 endpoints (method + path + body): no token → 401 `unauthorized`; admin token → 403 `forbidden` with the error shape; customer token is not 401/403 (implementation already delivered by T015, T018, T022, T025, T029)

**Checkpoint**: all user stories pass independently

---

## Phase 8: Polish & Cross-Cutting Concerns

- [ ] T032 Run `dotnet test backend --settings backend/coverage.runsettings --collect:"XPlat Code Coverage"`; all tests pass (AC 13); merge the per-project reports (scratch script) and confirm ≥ 80% line and branch coverage on every changed non-excluded file; report the numbers
- [ ] T033 Run the [quickstart.md](quickstart.md) validation steps 1–5 against the local SQL Server (applies `AddOrderRowVersion` and `AddOrderNumberSequence` to `OnlineStore`; records which local data changed) and measure the cart and checkout calls (SC-004, under 1 s)
- [ ] T034 Security/Data Access review of the 003 changes (constitution Quality Gates): only the sequence `SqlQuery` is raw SQL and it is interpolated without user input; no `DateTime.Now`/`UtcNow`; read-only queries `AsNoTracking` + `Select`; tracked loads only in cart writes, checkout and cancel; every endpoint `CustomerOnly`; ownership checks on every id; logs carry no shipping name, phone, address or notes; no exact stock in any response; no debug or commented-out code

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)** → **Foundational (Phase 2)** → user stories → **Polish (Phase 8)**
- **US1 (Phase 3)** depends on Foundational
- **US2 (Phase 4)** depends on US1 (carts are built through the cart API)
- **US3 (Phase 5)** depends on US2 (`OrderMapping`, `OrdersController`); its seeded-order tests do
  not need checkout
- **US4 (Phase 6)** depends on US1 (`CartReader`, `CartController`)
- **US5 (Phase 7)** depends on US1–US4 (all endpoints exist)

### Within Each Story

- Implementation first, then the story's tests; every commit builds
- Domain before persistence: T004–T009 before T010–T011; error/retry plumbing (T013) before any
  service

### Parallel Opportunities

- T002 alongside T001; T004, T006, T008 touch different entities (sequential commits, parallel work)
- T020 and T021 together
- US4 (Phase 6) in parallel with US2/US3 after US1 (different files except `CartController` —
  merge carefully)

---

## Parallel Example: User Story 1 tests

```text
Task: "CartLimitsTests (T020)"
Task: "CartAvailabilityTests (T021)"
```

---

## Implementation Strategy

### MVP First (User Story 1)

1. Phase 1 + Phase 2
2. Phase 3 (US1): server cart
3. Validate with US1 tests

### Incremental Delivery

1. US1 → cart
2. US2 → checkout (revenue path, concurrency proven)
3. US3 → own orders and cancel
4. US4 → guest merge
5. US5 → access guard tests
6. Polish → coverage, quickstart, security review

---

## Notes

- Migrations are applied only to the local SQL Server; the hosted database needs approval.
- API test classes that change stock, product state or seeded orders, or that send parallel
  requests, use their own database; others register fresh customers per test.

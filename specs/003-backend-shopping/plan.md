# Implementation Plan: Backend Shopping — Cart, Checkout & Customer Orders

**Branch**: `feat/003-backend-shopping` | **Date**: 2026-11-25 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/003-backend-shopping/spec.md`

**Tracking**: GitHub issue #7. Commits `<type> #003-T0xx <summary>`, one task per commit; the PR
into `develop` includes `Closes #7`.

## Summary

Add the customer's server cart (add/update/remove/clear, guest merge), checkout with cash on
delivery, and the customer's own orders (list, details, cancel). Stock rules and order transitions
move into the domain (`Deduct`, `Restock`, `CreatePending`, `Cancel`). Checkout and cancel run in a
transaction with the variant rowversion and up to 3 attempts, so the store never oversells. Order
numbers come from a SQL Server sequence starting at 1001. Customer-only access by role policy.

## Technical Context

**Language/Version**: C# 14 on .NET 10 (as 001/002)

**Primary Dependencies**: no new packages (EF Core 10.0.15, FluentValidation 12.2.0, JwtBearer
10.0.15, Swashbuckle 11.0.1 from 002)

**Storage**: SQL Server; new sequence `dbo.OrderNumberSequence` (migration `AddOrderNumberSequence`);
new `Orders.RowVersion` concurrency token (migration `AddOrderRowVersion`)

**Testing**: xUnit, Shouldly, coverlet; `Store.API.Tests` (WebApplicationFactory, one seeded
database per class), `Store.Domain.Tests`, `Store.Infrastructure.Tests`

**Target Platform**: ASP.NET Core Web API on Windows/IIS (MonsterASP.NET)

**Project Type**: web service (backend only)

**Performance Goals**: cart and checkout calls under 1 s locally (SC-004); checkout = a few queries
plus one save per attempt

**Constraints**: never oversell (rowversion + transaction + retry); no raw SQL except the
interpolated `SqlQuery` for the sequence; exact stock never returned; customer-only

**Scale/Scope**: 9 endpoints; 4 domain entities changed; 1 sequence; 1 new column

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle / rule | Status | How |
|---|---|---|
| I. Rules in Domain/Application, not controllers | ✅ | `Deduct`/`Restock`, `CartItem` range, `CreatePending`, `Cancel` in Domain; customer Pending-only rule in `CustomerOrderService` |
| I. `Order` owns transitions and items, rejects invalid ones | ✅ | `CreatePending` / `Cancel` + `DomainException` ([R1](research.md#r1-domain-rules)) |
| I. DB constraints are only a safety net | ✅ | stock ≥ 0, quantity 1–5, unique cart line all checked in code first |
| I. No MediatR/CQRS/events; feature folders | ✅ | `Cart/`, `Orders/` with services, DTOs, validators |
| II. `IAppDbContext`, no repository | ✅ | `ChangeTracker` added to the interface for retries |
| II. Read-only queries `AsNoTracking` + `Select` | ✅ | cart read, order list/details; tracked loads only for checkout, cancel and cart writes |
| II. Named migration; one stored procedure | ✅ | `AddOrderNumberSequence` (a sequence, not a procedure), `AddOrderRowVersion` |
| III. Raw SQL only via interpolated `SqlQuery`/`FromSql` | ✅ | `SqlQuery<long>($"SELECT NEXT VALUE FOR ...")` in Infrastructure ([R4](research.md#r4-order-numbers)) |
| III. Role-based authorization | ✅ | `CustomerOnly` policy ([R2](research.md#r2-customer-only-authorization)) |
| IV. Stock per variant, rowversion, `Stock >= 0` | ✅ | 001 schema + `Deduct` |
| IV. Checkout in a single transaction; conflict → retry, `cartChanged` / `checkoutConflict`, never oversells (1.7.3) | ✅ | [R5](research.md#r5-checkout-algorithm-critical-area) |
| V. Money decimal; UTC; order item snapshots | ✅ | snapshots at checkout; `TimeProvider` |
| Business: max 5, shipping fee from configuration, customer cancels Pending only, cancel returns stock | ✅ | `StoreLimits`, `ShopOptions`, [R6](research.md#r6-cancel-algorithm) |
| API: one error shape, codes as constants | ✅ | optional `details` added ([R10](research.md#r10-error-details)) |
| AI-Assisted: explain checkout/concurrency/transitions + required tests | ✅ | R5/R6 explanation; concurrency + transition tests |
| Quality Gates: checkout concurrency and order transition tests | ✅ | AC 6/7/8 tests; `OrderTests` |
| Workflow: approved dependencies; 80% coverage; backend only | ✅ | no new packages |

**Result**: PASS (constitution 1.7.3). Re-checked after Phase 1: no change.

## Project Structure

### Documentation (this feature)

```text
specs/003-backend-shopping/
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
│   ├── Store.Domain/
│   │   ├── StoreLimits.cs                   ✚ MaxQuantityPerVariant (moved), MaxCartLines
│   │   ├── DomainException.cs               ✚
│   │   └── Entities/                        ✎ ProductVariant (Deduct/Restock), CartItem (1–5),
│   │                                          Order (CreatePending, Cancel, RowVersion)
│   ├── Store.Application/
│   │   ├── Common/
│   │   │   ├── CatalogLimits.cs             ✖ removed (→ Domain StoreLimits)
│   │   │   ├── IAppDbContext.cs             ✎ + ChangeTracker
│   │   │   ├── ShopOptions.cs               ✚ ShippingFee + validator
│   │   │   ├── ConcurrencyRetry.cs          ✚ 3-attempt shell (checkout, cancel)
│   │   │   ├── IOrderNumberGenerator.cs     ✚
│   │   │   └── Errors/                      ✎ new codes; AppException.Details; BadRequestException
│   │   ├── Cart/                            ✚ CartReader (cart query), CartService (add/update/remove/clear),
│   │   │                                      CartMergeService, DTOs, validators
│   │   ├── Orders/                          ✚ CheckoutService, CustomerOrderService, DTOs, validators
│   │   └── Products/ProductService.cs       ✎ uses StoreLimits
│   ├── Store.Infrastructure/
│   │   ├── Orders/OrderNumberGenerator.cs   ✚ sequence via SqlQuery
│   │   ├── Persistence/AppDbContext.cs      ✎ HasSequence
│   │   ├── Persistence/Configurations/      ✎ OrderConfiguration (RowVersion IsRowVersion)
│   │   ├── Persistence/Migrations/          ✚ AddOrderNumberSequence, AddOrderRowVersion
│   │   └── DependencyInjection.cs           ✎ generator, ShopOptions
│   └── Store.API/
│       ├── Controllers/                     ✚ CartController, OrdersController
│       ├── Authorization/Policies.cs        ✚ CustomerOnly
│       ├── Errors/                          ✎ ErrorResponse.Details, handler writes it
│       ├── Program.cs                       ✎ policy, enum-as-string JSON, ShopOptions fail-fast
│       └── appsettings.json                 ✎ "Shop": { "ShippingFee": 50 }
└── tests/
    ├── Store.Domain.Tests/Entities/         ✚ ProductVariantTests, CartItemTests; ✎ OrderTests
    ├── Store.Infrastructure.Tests/          ✚ Orders/OrderNumberGeneratorTests, Orders/CancelOrderRaceTests;
    │                                          ✎ SchemaTests (sequence, Orders.RowVersion)
    └── Store.API.Tests/
        ├── Fixtures/ShoppingClient.cs       ✚ cart/order helpers, variant id lookup by SKU
        ├── ShoppingAccessTests.cs           US5, AC 1
        ├── CartTests.cs, CartLimitsTests.cs US1, AC 2, 3
        ├── CartAvailabilityTests.cs         US1-5/6, AC 9 (own DB: deactivates/reprices)
        ├── CartMergeTests.cs                US4, AC 4
        ├── CheckoutTests.cs                 US2-1/5/6/7, AC 5, 9, 12
        ├── CheckoutConcurrencyTests.cs      AC 6, 7, 8 (own DB per test class)
        ├── CustomerOrdersTests.cs           US3-1/2, AC 10
        └── CancelOrderTests.cs              US3-3/4, AC 11 (own DB: changes seeded orders)
```

**Structure Decision**: extend the 002 layout with `Cart/` and `Orders/` feature folders; no new
project. `CartReader` builds the cart response (one projection, R7) and is shared by
`CartService` and `CartMergeService`, which keeps each service small and under Sonar's 7-parameter
constructor limit.

## Key design points

- **Checkout** ([R5](research.md#r5-checkout-algorithm-critical-area)) and **cancel**
  ([R6](research.md#r6-cancel-algorithm)) share `ConcurrencyRetry`: 3 attempts, each with a clean
  change tracker and its own transaction; `DbUpdateConcurrencyException` → next attempt. Cancel is
  guarded by both the variant rowversions and the new `Orders.RowVersion`, so a status change made
  elsewhere between read and save (004 admin confirm) forces a re-read → `orderCannotBeCancelled`.
- **Availability** ([R7](research.md#r7-cart-availability-and-projection)) is computed in SQL;
  stock never leaves the database; `maxQuantity = min(stock, 5)`.
- **Order numbers** ([R4](research.md#r4-order-numbers)) come from `IOrderNumberGenerator`
  (Infrastructure) because `SqlQuery` lives in the relational EF package that Application does not
  reference.
- **Errors**: 11 new codes ([R11](research.md#r11-error-codes-added)); structured `details` for
  `insufficientStock` and `cartChanged`; domain exceptions translated by services.
- **Test isolation**: API classes that change shared data (stock, seeded orders, product state) or
  run parallel requests get their own database; others register fresh customers per test.
- **Logging**: user id, order id/number, item counts, attempt number, outcome code — never
  shipping name, phone, address or notes (FR-004 of 002).

## Acceptance criteria → tests

| AC | Test class |
|---|---|
| 1 | ShoppingAccessTests |
| 2, 3 | CartTests, CartLimitsTests |
| 4 | CartMergeTests |
| 5, 12 | CheckoutTests |
| 6, 7, 8 | CheckoutConcurrencyTests |
| 9 | CartAvailabilityTests, CheckoutTests |
| 10 | CustomerOrdersTests |
| 11 | CancelOrderTests; CancelOrderRaceTests (stale Pending read vs. a status change) |
| 13 | full suite |

## Complexity Tracking

No constitution violations requiring justification.

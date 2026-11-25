# Data Model: Backend Shopping

Changes on top of [001-database](../001-database/data-model.md) and
[002-backend-core](../002-backend-core/data-model.md). No new table; one new sequence; one new
column (`Orders.RowVersion`); domain behavior added to existing entities.

## New column: `Orders.RowVersion`

| Column | Type | Rules |
|---|---|---|
| Order.RowVersion | rowversion (`byte[]`), not null | concurrency token (`IsRowVersion`); changes on every update of the order |

Added by the named migration **`AddOrderRowVersion`** (existing rows get values automatically).
Protects status changes: a cancel based on a stale read fails and is retried (research R6).

## New: `dbo.OrderNumberSequence`

| Property | Value |
|---|---|
| Type | bigint |
| Start | 1001 (above the seeded ORD-0001…ORD-0008) |
| Increment | 1 |

Created by the named migration **`AddOrderNumberSequence`** (`HasSequence<long>` in the model).
Order number format: `ORD-{value}` (fits `Orders.OrderNumber` nvarchar(20), unique index from 001).

## New: `StoreLimits` (Store.Domain)

- `MaxQuantityPerVariant = 5` — replaces `Store.Application/Common/CatalogLimits` (002).
- `MaxCartLines = 30`.

## New: `DomainException` (Store.Domain)

Thrown when an entity rule is broken; application services translate it (research R1).

## Changed: ProductVariant

| Member | Rule |
|---|---|
| `Deduct(int quantity)` | ProductVariant.Stock: quantity ≥ 1, else `DomainException`; `Stock - quantity` < 0 → `DomainException`, stock unchanged |
| `Restock(int quantity)` | quantity ≥ 1, else `DomainException`; `Stock += quantity` |

ProductVariant.RowVersion (001) is the concurrency token for both.

## Changed: CartItem

| Member | Rule |
|---|---|
| constructor | CartItem.Quantity 1–`StoreLimits.MaxQuantityPerVariant`, else `DomainException` |
| `SetQuantity(int quantity)` | same range |

Unique `(UserId, ProductVariantId)` and `CK_CartItems_Quantity` (001) stay as the safety net.

## Changed: Order

| Member | Rule |
|---|---|
| `CreatePending(orderNumber, userId, shippingName, shippingPhone, city, addressLine, notes, items, shippingFee, now)` | order number not empty; at least one item; shipping fee ≥ 0; Order.Subtotal = Σ OrderItem.UnitPrice × OrderItem.Quantity; Order.Total = Subtotal + ShippingFee; Order.Status = Pending; Order.CreatedAt = Order.UpdatedAt = now |
| `Cancel(DateTime now)` | Order.Status Pending or Confirmed → Cancelled, Order.UpdatedAt = now; any other status → `DomainException` |
| `CreateHistorical` (001) | unchanged (seeder only) |
| `RowVersion` | `byte[]`, private setter, set by the database |

### Order status transitions (this feature)

```text
(new) ──CreatePending──▶ Pending ──Cancel──▶ Cancelled
                         Confirmed ──Cancel──▶ Cancelled      (domain allows; customer may not)
Shipped / Delivered / Cancelled ──Cancel──▶ DomainException
```

Confirm / ship / deliver arrive in 004.

## Changed: OrderItem

No new behavior; created only through `Order.CreatePending` / `CreateHistorical`. Snapshot fields
(OrderItem.ProductName, OrderItem.ColorName, OrderItem.SizeName, OrderItem.UnitPrice,
OrderItem.Quantity) come from current catalog data at checkout.

## Application contracts

- `ShopOptions.ShippingFee` (decimal, ≥ 0, default 50) — `Shop:ShippingFee`.
- `IOrderNumberGenerator.NextAsync()` → `string` (`ORD-1001`, …).
- `IAppDbContext` gains `ChangeTracker ChangeTracker { get; }`.
- DTOs: see [contracts/api.md](contracts/api.md).

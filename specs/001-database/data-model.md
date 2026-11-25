# Data Model: Store Database & Seed Data (001-database)

All keys are `int` identity unless stated. All `DateTime` values are UTC, column type
`datetime2`. Money is `decimal(18,2)`. Enums are stored as `nvarchar(20)` strings. All strings
are required unless marked *nullable*. Entities expose private setters and constructors that take
their required values; they hold data only (no status-transition logic in this feature).

## Enums

- **Role**: `Admin`, `Customer`
- **OrderStatus**: `Pending`, `Confirmed`, `Shipped`, `Delivered`, `Cancelled`

## Entities

### User (`Users`)

| Field | Type | Rules |
|---|---|---|
| Id | int | PK |
| FullName | nvarchar(100) | |
| Email | nvarchar(256) | unique |
| Phone | nvarchar(20) | |
| PasswordHash | nvarchar(max) | output of `PasswordHasher<User>` |
| Role | nvarchar(20) | enum `Role` |
| CreatedAt | datetime2 | |

Relationships: Orders (Restrict), CartItems (Cascade).

### Category (`Categories`)

| Field | Type | Rules |
|---|---|---|
| Id | int | PK |
| Name | nvarchar(100) | |
| Slug | nvarchar(200) | unique |
| ParentCategoryId | int | *nullable*, FK → Categories (NoAction) |

The one-level nesting limit is an application rule, not enforced here (FR-005).
Relationships: Products (Restrict), child Categories (NoAction).

### Product (`Products`)

| Field | Type | Rules |
|---|---|---|
| Id | int | PK |
| CategoryId | int | FK → Categories (Restrict) |
| Name | nvarchar(200) | |
| Slug | nvarchar(200) | unique |
| Description | nvarchar(2000) | |
| Price | decimal(18,2) | check `CK_Products_Price`: `Price > 0` |
| IsActive | bit | |
| IsFeatured | bit | |
| CreatedAt | datetime2 | |

Indexes: `IX_Products_CategoryId`; `IX_Products_IsActive_IsFeatured`.
Relationships: Variants (Cascade), Images (Cascade).

### Color (`Colors`) — seeded with `HasData`, Ids 1–8

| Field | Type | Rules |
|---|---|---|
| Id | int | PK (fixed values, no identity insert issues) |
| Name | nvarchar(50) | unique |
| HexCode | nvarchar(7) | |

### Size (`Sizes`) — seeded with `HasData`, Ids 1–14

| Field | Type | Rules |
|---|---|---|
| Id | int | PK |
| Name | nvarchar(10) | unique |
| DisplayOrder | int | |

### ProductVariant (`ProductVariants`)

| Field | Type | Rules |
|---|---|---|
| Id | int | PK |
| ProductId | int | FK → Products (Cascade) |
| ColorId | int | FK → Colors (Restrict) |
| SizeId | int | FK → Sizes (Restrict) |
| Sku | nvarchar(50) | unique |
| Stock | int | check `CK_ProductVariants_Stock`: `Stock >= 0` |
| RowVersion | rowversion | `IsRowVersion()`, concurrency token |

Unique index: `(ProductId, ColorId, SizeId)`.
Relationships: CartItems (Cascade), OrderItems (Restrict).

### ProductImage (`ProductImages`)

| Field | Type | Rules |
|---|---|---|
| Id | int | PK |
| ProductId | int | FK → Products (Cascade) |
| ColorId | int | *nullable*, FK → Colors (Restrict); null = general image |
| Url | nvarchar(500) | e.g. `/images/products/classic-cotton-t-shirt-black.jpg` |
| DisplayOrder | int | |
| IsMain | bit | |

### CartItem (`CartItems`)

| Field | Type | Rules |
|---|---|---|
| Id | int | PK |
| UserId | int | FK → Users (Cascade) |
| ProductVariantId | int | FK → ProductVariants (Cascade) |
| Quantity | int | check `CK_CartItems_Quantity`: `Quantity BETWEEN 1 AND 5` |
| AddedAt | datetime2 | |

Unique index: `(UserId, ProductVariantId)`.

### Order (`Orders`)

| Field | Type | Rules |
|---|---|---|
| Id | int | PK |
| OrderNumber | nvarchar(20) | unique, e.g. `ORD-0001` |
| UserId | int | FK → Users (Restrict) |
| Status | nvarchar(20) | enum `OrderStatus` |
| Subtotal | decimal(18,2) | |
| ShippingFee | decimal(18,2) | |
| Total | decimal(18,2) | not checked against Subtotal + ShippingFee (spec assumption) |
| ShippingName | nvarchar(100) | |
| ShippingPhone | nvarchar(20) | |
| City | nvarchar(100) | |
| AddressLine | nvarchar(300) | |
| Notes | nvarchar(500) | *nullable* |
| CreatedAt | datetime2 | |
| UpdatedAt | datetime2 | |

Indexes: `IX_Orders_UserId`, `IX_Orders_Status`, `IX_Orders_CreatedAt`.
Relationships: OrderItems (Cascade).

### OrderItem (`OrderItems`)

| Field | Type | Rules |
|---|---|---|
| Id | int | PK |
| OrderId | int | FK → Orders (Cascade) |
| ProductVariantId | int | FK → ProductVariants (Restrict) |
| ProductName | nvarchar(200) | snapshot |
| ColorName | nvarchar(50) | snapshot |
| SizeName | nvarchar(10) | snapshot |
| UnitPrice | decimal(18,2) | snapshot |
| Quantity | int | check `CK_OrderItems_Quantity`: `Quantity BETWEEN 1 AND 5` |

Unique index: `(OrderId, ProductVariantId)`.

## Requirement mapping

| Requirement | Where |
|---|---|
| FR-003 unique values | unique indexes on Email, Slug (×2), Color/Size Name, Sku, OrderNumber |
| FR-004 unique combinations | unique indexes on ProductVariants, CartItems, OrderItems |
| FR-007/008/009 | check constraints above |
| FR-010/011/012/013 | delete behaviors above |
| FR-014 | `ProductVariants.RowVersion` |
| FR-015 | indexes on Products and Orders above |

## Seed group mapping

| Group | Contents | Condition |
|---|---|---|
| Schema (`HasData`) | Colors (8), Sizes (14) | part of `InitialCreate` |
| Accounts | admin, demo customer | per account, if email not found |
| Catalog + demo orders | 12 categories, 18 products, 177 variants, 37 images, 8 orders, 12 order items | only if `Products` is empty; one transaction |

Stock rule for non-special variants: see [research.md R9](research.md#r9-deterministic-stock-rule).

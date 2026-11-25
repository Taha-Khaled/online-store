# API Contract: Backend Shopping

Base path `/api`, JSON camelCase, dates ISO 8601 UTC. Every endpoint below requires
`Authorization: Bearer <accessToken>` of a **Customer**: no/invalid token → 401 `unauthorized`;
any other role → 403 `forbidden`.

## Error shape (extended)

```json
{ "code": "cartChanged", "message": "Some items in your cart changed.",
  "details": { "items": [ { "itemId": 41, "variantId": 12, "status": "quantityReduced", "maxQuantity": 1 } ] } }
```

`details` is optional and omitted when null; 002 errors never include it. `errors` (field
validation) is unchanged.

| Code | Status | Details |
|---|---|---|
| `cartEmpty` | 400 | — |
| `maxQuantityExceeded` | 400 | — |
| `cartItemNotFound` | 404 | — |
| `variantNotFound` | 404 | — |
| `orderNotFound` | 404 | — |
| `insufficientStock` | 409 | `{ "variantId", "maxQuantity" }` |
| `cartFull` | 409 | — |
| `cartChanged` | 409 | `{ "items": [{ "itemId", "variantId", "status", "maxQuantity" }] }` |
| `checkoutConflict` | 409 | — |
| `orderCannotBeCancelled` | 409 | — |
| `orderConflict` | 409 | — |

## Cart

### `GET /api/cart` → 200 `Cart`

```json
{
  "items": [ {
    "itemId": 41, "variantId": 87, "productName": "Basic Ribbed Top", "slug": "basic-ribbed-top",
    "colorName": "White", "sizeName": "S", "unitPrice": 300.00, "lineTotal": 600.00,
    "imageUrl": "/images/products/basic-ribbed-top-white.jpg",
    "quantity": 2, "maxQuantity": 5, "status": "available" } ],
  "itemCount": 1, "subtotal": 600.00, "shippingFee": 50.00, "total": 650.00, "canCheckout": true
}
```

`status`: `available` | `quantityReduced` | `outOfStock` | `unavailable`. `subtotal` counts
`available` lines only; `shippingFee` is 0 when the subtotal is 0. `itemCount` = number of lines.

### `POST /api/cart/items` → 200 `Cart`

Body `{ "variantId": 87, "quantity": 2 }` (quantity 1–5). Adds to an existing line.
Errors: 400 validation / `maxQuantityExceeded`, 404 `variantNotFound`, 409 `insufficientStock` /
`cartFull`.

### `PUT /api/cart/items/{itemId}` → 200 `Cart`

Body `{ "quantity": 3 }` (1–5). Errors: 400, 404 `cartItemNotFound` / `variantNotFound`, 409
`insufficientStock`.

### `DELETE /api/cart/items/{itemId}` → 200 `Cart`

Errors: 404 `cartItemNotFound`.

### `DELETE /api/cart` → 204

### `POST /api/cart/merge` → 200

Body `{ "items": [ { "variantId": 12, "quantity": 1 } ] }` (≤ 30 entries, quantity 1–5).

```json
{ "cart": { ...Cart... },
  "adjustments": [
    { "variantId": 13, "reason": "outOfStock", "requestedQuantity": 1, "quantity": 0 },
    { "variantId": 99999, "reason": "variantNotFound", "requestedQuantity": 1, "quantity": 0 } ] }
```

`reason`: `variantNotFound` | `outOfStock` | `cartFull` | `quantityReduced`. Skipped entries have
`quantity` 0; `quantityReduced` shows the quantity actually set.

## Orders

### `POST /api/orders` → 201 `OrderDetails` (checkout)

```json
{ "shippingName": "Demo Customer", "shippingPhone": "+201000000002", "city": "Cairo",
  "addressLine": "12 Tahrir Street, Downtown", "notes": "Call before delivery" }
```

Rules: name 2–100, phone ≤ 20 ASCII digits with optional leading `+`, city 2–100, address 5–300,
notes ≤ 500 (optional). Strings are trimmed. Location header `/api/orders/{id}`.
Errors: 400 validation / `cartEmpty`; 409 `cartChanged` / `checkoutConflict`.

### `GET /api/orders?page=1&pageSize=10` → 200

```json
{ "items": [ { "id": 9, "orderNumber": "ORD-1001", "status": "Pending", "itemCount": 1,
               "total": 650.00, "createdAt": "2026-11-24T12:00:00Z" } ],
  "page": 1, "pageSize": 10, "totalCount": 1, "totalPages": 1 }
```

Newest first. page ≥ 1, pageSize 1–50 (400 otherwise). `itemCount` = number of order lines.

### `GET /api/orders/{id}` → 200 `OrderDetails`

```json
{ "id": 9, "orderNumber": "ORD-1001", "status": "Pending",
  "createdAt": "...", "updatedAt": "...",
  "shippingName": "...", "shippingPhone": "...", "city": "...", "addressLine": "...", "notes": null,
  "items": [ { "productVariantId": 87, "productName": "Basic Ribbed Top", "colorName": "White",
               "sizeName": "S", "unitPrice": 300.00, "quantity": 2, "lineTotal": 600.00 } ],
  "subtotal": 600.00, "shippingFee": 50.00, "total": 650.00, "canCancel": true }
```

Errors: 404 `orderNotFound` (unknown or another customer's).

### `POST /api/orders/{id}/cancel` → 200 `OrderDetails`

Pending only. Errors: 404 `orderNotFound`; 409 `orderCannotBeCancelled` / `orderConflict`.

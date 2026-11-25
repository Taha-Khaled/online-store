# API Contract: Backend Core

Base path `/api`. JSON, camelCase, dates ISO 8601 UTC (`Z`). Authentication: `Authorization:
Bearer <accessToken>` only; the API never sets cookies.

## Error shape (every error)

```json
{ "code": "validationFailed", "message": "One or more fields are invalid.",
  "errors": { "email": ["Email is required."] } }
```

`errors` appears only for `validationFailed`.

| Code | Status | When |
|---|---|---|
| `validationFailed` | 400 | FluentValidation failure or model binding error (bad number, unknown `sort`, malformed JSON, missing body) |
| `unauthorized` | 401 | missing / invalid / expired access token; `/me` user no longer exists |
| `invalidCredentials` | 401 | login: unknown email or wrong password (same response) |
| `invalidRefreshToken` | 401 | refresh: unknown, expired, revoked or rotated token |
| `forbidden` | 403 | authenticated but not allowed (no endpoint uses it yet) |
| `notFound` | 404 | unknown route or missing image file |
| `categoryNotFound` | 404 | unknown category slug |
| `productNotFound` | 404 | unknown or inactive product slug |
| `methodNotAllowed` | 405 | wrong HTTP method on a known route |
| `emailAlreadyExists` | 409 | registration with an existing email (any letter case) |
| `unsupportedMediaType` | 415 | body is not JSON |
| `requestFailed` | other 4xx | any other framework status without a body |
| `internalError` | 500 | unexpected failure; generic message, details only in logs |

All codes live in `Store.Application/Common/Errors/ErrorCodes.cs`.

## Auth

### `POST /api/auth/register` → 201

Request: `{ "fullName", "email", "phone", "password" }` (rules: spec FR-013).
Response: `AuthResponse` (below). Always creates role `Customer`. Errors: 400, 409
`emailAlreadyExists`.

### `POST /api/auth/login` → 200

Request: `{ "email", "password" }` (both required). Response: `AuthResponse`.
Errors: 400, 401 `invalidCredentials`.

```json
{
  "accessToken": "eyJ...",
  "accessTokenExpiresAt": "2026-11-24T12:15:00Z",
  "refreshToken": "x7Q...86 chars",
  "refreshTokenExpiresAt": "2026-12-01T12:00:00Z",
  "user": { "id": 2, "fullName": "Demo Customer", "email": "customer@store.local", "role": "Customer" }
}
```

### `POST /api/auth/refresh` → 200

Request: `{ "refreshToken" }`. Response: `AuthResponse` with a new pair; the presented token is
rotated. Errors: 400 (empty token), 401 `invalidRefreshToken`. Reusing a rotated token also
revokes every active refresh token of that user.

### `POST /api/auth/logout` → 204

Request: `{ "refreshToken" }`. Revokes that token if active; otherwise does nothing. Always 204
for a non-empty token (400 if empty). No access token needed.

### `GET /api/auth/me` → 200 *(Bearer required)*

Response: `{ "id", "fullName", "email", "role" }`. Errors: 401 `unauthorized`.

Access token claims: `sub` (user id), `email`, `role`, `jti`, `iat`, `nbf`, `exp` (15 min),
`iss`, `aud`.

## Catalog (public)

### `GET /api/categories` → 200

```json
[ { "name": "Men", "slug": "men",
    "children": [ { "name": "T-Shirts", "slug": "men-t-shirts" }, ... ] }, ... ]
```

Parents ordered by `Id` (seed order Men, Women, Shoes); children by `Id`.

### `GET /api/products` → 200

| Query | Type | Default | Rules |
|---|---|---|---|
| `search` | string | — | trimmed; empty ignored; ≤ 100 |
| `category` | slug | — | parent includes children; unknown → 404 `categoryNotFound` |
| `color` | string, repeatable | — | case-insensitive names |
| `size` | string, repeatable | — | case-insensitive names |
| `minPrice`, `maxPrice` | decimal | — | ≥ 0; min ≤ max |
| `featured` | bool | false | true → featured only |
| `sort` | `newest` \| `priceAsc` \| `priceDesc` \| `nameAsc` | `newest` | anything else → 400 |
| `page` | int | 1 | ≥ 1 |
| `pageSize` | int | 12 | 1–48 |

```json
{
  "items": [ {
    "id": 1, "name": "Classic Cotton T-Shirt", "slug": "classic-cotton-t-shirt",
    "price": 350.00, "categoryName": "T-Shirts", "isFeatured": true,
    "mainImageUrl": "/images/products/classic-cotton-t-shirt-black.jpg",
    "colors": [ { "name": "Black", "hexCode": "#000000" }, ... ],
    "inStock": true } ],
  "page": 1, "pageSize": 12, "totalCount": 18, "totalPages": 2
}
```

`mainImageUrl` is null when the product has no main image.

### `GET /api/products/{slug}` → 200

```json
{
  "id": 1, "name": "...", "slug": "...", "description": "...", "price": 350.00,
  "category": { "name": "T-Shirts", "slug": "men-t-shirts", "parentName": "Men" },
  "colors": [ { "id": 1, "name": "Black", "hexCode": "#000000",
                "images": [ { "url": "/images/products/...-black.jpg", "isMain": true } ] } ],
  "sizes": [ { "id": 2, "name": "S" }, ... ],
  "variants": [ { "id": 10, "colorId": 1, "sizeId": 2, "sku": "MTS-001-BLK-S",
                  "inStock": true, "maxQuantity": 5 } ]
}
```

`maxQuantity = min(stock, 5)` (0 when out of stock). Stock is never returned. Errors: 404
`productNotFound`.

### `GET /api/products/filters` → 200

Query: `category` (optional slug; unknown → 404 `categoryNotFound`).

```json
{ "colors": [ { "name": "Beige", "hexCode": "#..." } ],
  "sizes": [ { "name": "XS" }, { "name": "S" } ],
  "minPrice": 300.00, "maxPrice": 2200.00 }
```

Colors by name; sizes by display order; prices null when no active product is in scope.

## Images (public, outside `/api`)

`GET /images/products/<file>` → the file from the seed images folder; missing → 404 `notFound`.

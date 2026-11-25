# Contract: `dbo.sp_GetDashboardStats`

Created in migration `AddDashboardStatsProcedure` (`migrationBuilder.Sql`), dropped in `Down`.
No dynamic SQL; parameters only.

## Parameters

| Name | Type | Default | Meaning |
|---|---|---|---|
| `@FromUtc` | `datetime2` | — | range start, **inclusive** (UTC) |
| `@ToUtc` | `datetime2` | — | range end, **exclusive** (UTC) |
| `@LowStockThreshold` | `int` | `3` | variants with `1 <= Stock <= threshold` are low stock |

Orders are filtered by `CreatedAt >= @FromUtc AND CreatedAt < @ToUtc`.

## Result

Exactly one row with one column named `Value` (`nvarchar(max)`) containing one JSON object.
The JSON is built into a variable before being selected, so it is never split across rows.

```json
{
  "totalRevenue": 5200.00,
  "totalOrders": 8,
  "ordersByStatus": {
    "pending": 2, "confirmed": 1, "shipped": 1, "delivered": 3, "cancelled": 1
  },
  "bestSellers": [
    { "productId": 1, "productName": "Classic Cotton T-Shirt", "unitsSold": 4 }
  ],
  "lowStockVariants": [
    { "variantId": 2, "productName": "Classic Cotton T-Shirt", "colorName": "Black",
      "sizeName": "M", "sku": "MTS-001-BLK-M", "stock": 1 }
  ]
}
```

| Field | Rule |
|---|---|
| `totalRevenue` | `SUM(Total)` of `Delivered` orders in range; `0` when none |
| `totalOrders` | count of all orders in range, any status |
| `ordersByStatus` | all five keys always present; `0` when none |
| `bestSellers` | top 5 products by `SUM(Quantity)` from non-`Cancelled` orders in range; order: `unitsSold` desc, `productName` asc; `[]` when none |
| `lowStockVariants` | all variants with `1 <= Stock <= @LowStockThreshold`, ignoring the range; order: `stock` asc, `productName` asc, `colorName` asc, size `DisplayOrder` asc; `[]` when none |

Lists are never `null` (`JSON_QUERY(ISNULL(<subquery FOR JSON PATH>, '[]'))`).

## Expected results on seed data (seed time `T`, range `[T − 30 days, T + 1 minute)`, threshold 3)

- `totalRevenue` 5200.00, `totalOrders` 8, statuses as above.
- `bestSellers`: Classic Cotton T-Shirt 4, Basic Ribbed Top 3, Classic Denim Jeans 1,
  Everyday Slides 1, Everyday White Sneakers 1.
- `lowStockVariants`: Classic Cotton T-Shirt Black/M 1, Leather Derby Shoes Brown/44 1,
  Floral Midi Dress Red/S 2, Everyday White Sneakers White/43 3.

Range with no orders (e.g. `[T + 1 day, T + 2 days)`): revenue 0, all counts 0,
`bestSellers` `[]`, `lowStockVariants` unchanged.

## Caller

```csharp
var json = (await db.Database
    .SqlQuery<string>($"EXEC dbo.sp_GetDashboardStats @FromUtc = {fromUtc}, @ToUtc = {toUtc}, @LowStockThreshold = {threshold}")
    .ToListAsync(ct))
    .Single();
```

Do not apply LINQ operators before materializing (an `EXEC` cannot be composed). Deserialize
with `System.Text.Json` (`JsonSerializerDefaults.Web`). In this feature only the integration tests
call it; the dashboard service is a later feature.

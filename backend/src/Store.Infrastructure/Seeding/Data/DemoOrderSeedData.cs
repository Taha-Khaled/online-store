using Store.Domain.Enums;

namespace Store.Infrastructure.Seeding.Data;

public sealed record OrderItemSeed(string ProductSlug, string Color, string Size, int Quantity);

public sealed record OrderSeed(string OrderNumber, int DaysAgo, OrderStatus Status, IReadOnlyList<OrderItemSeed> Items);

/// <summary>Demo order history for the demo customer (spec.md "Demo orders").</summary>
public static class DemoOrderSeedData
{
    /// <summary>Historical fee stored on every demo order; seeded totals must match the spec.</summary>
    public const decimal ShippingFee = 50m;
    public const string City = "Cairo";
    public const string AddressLine = "12 Tahrir Street, Downtown";

    public static readonly IReadOnlyList<OrderSeed> Orders =
    [
        new("ORD-0001", 28, OrderStatus.Delivered,
        [
            new("classic-denim-jeans", "Blue", "M", 1),
            new("classic-cotton-t-shirt", "White", "M", 2)
        ]),
        new("ORD-0002", 24, OrderStatus.Delivered,
        [
            new("everyday-white-sneakers", "White", "41", 1)
        ]),
        new("ORD-0003", 20, OrderStatus.Cancelled,
        [
            new("floral-midi-dress", "Navy", "M", 1)
        ]),
        new("ORD-0004", 15, OrderStatus.Delivered,
        [
            new("oxford-button-down-shirt", "White", "L", 1),
            new("slim-fit-chinos", "Beige", "L", 1)
        ]),
        new("ORD-0005", 9, OrderStatus.Shipped,
        [
            new("leather-derby-shoes", "Black", "42", 1)
        ]),
        new("ORD-0006", 5, OrderStatus.Confirmed,
        [
            new("basic-ribbed-top", "Black", "S", 3),
            new("wide-leg-trousers", "Black", "S", 1)
        ]),
        new("ORD-0007", 2, OrderStatus.Pending,
        [
            new("graphic-oversized-tee", "Gray", "L", 1),
            new("everyday-slides", "Black", "42", 1)
        ]),
        new("ORD-0008", 0, OrderStatus.Pending,
        [
            new("classic-cotton-t-shirt", "Black", "L", 2)
        ])
    ];
}

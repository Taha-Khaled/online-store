namespace Store.Application.Cart.Responses;

/// <summary>The customer's cart (contracts/api.md "GET /api/cart").</summary>
public sealed record CartResponse(
    IReadOnlyList<CartItemResponse> Items,
    int ItemCount,
    decimal Subtotal,
    decimal ShippingFee,
    decimal Total,
    bool CanCheckout);

/// <summary>One cart line. Exact stock is never exposed; only <see cref="MaxQuantity"/> and <see cref="Status"/>.</summary>
public sealed record CartItemResponse(
    int ItemId,
    int VariantId,
    string ProductName,
    string Slug,
    string ColorName,
    string SizeName,
    decimal UnitPrice,
    decimal LineTotal,
    string? ImageUrl,
    int Quantity,
    int MaxQuantity,
    CartItemStatus Status);

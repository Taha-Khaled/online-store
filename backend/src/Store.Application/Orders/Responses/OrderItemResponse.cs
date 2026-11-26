namespace Store.Application.Orders.Responses;

/// <summary>An order line as stored at checkout (snapshot fields, contracts/api.md).</summary>
public sealed record OrderItemResponse(
    int ProductVariantId,
    string ProductName,
    string ColorName,
    string SizeName,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal);

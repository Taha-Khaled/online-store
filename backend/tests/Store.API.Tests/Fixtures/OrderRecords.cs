using System.Globalization;

namespace Store.API.Tests.Fixtures;

/// <summary>Test-local shapes of the order responses (contracts/api.md "Orders").</summary>
public sealed record OrderLine(
    int ProductVariantId,
    string ProductName,
    string ColorName,
    string SizeName,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal);

public sealed record OrderBody(
    int Id,
    string OrderNumber,
    string Status,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string ShippingName,
    string ShippingPhone,
    string City,
    string AddressLine,
    string? Notes,
    List<OrderLine> Items,
    decimal Subtotal,
    decimal ShippingFee,
    decimal Total,
    bool CanCancel);

public static class OrderRecords
{
    public static Task<OrderBody> ReadOrderAsync(this HttpResponseMessage response) => response.ReadJsonAsync<OrderBody>();

    /// <summary>The numeric part of <c>ORD-1001</c>.</summary>
    public static long NumberOf(string orderNumber) =>
        long.Parse(orderNumber["ORD-".Length..], CultureInfo.InvariantCulture);
}

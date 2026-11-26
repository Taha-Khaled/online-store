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

public sealed record OrderSummary(int Id, string OrderNumber, string Status, int ItemCount, decimal Total, DateTime CreatedAt);

public sealed record OrderPage(List<OrderSummary> Items, int Page, int PageSize, int TotalCount, int TotalPages);

public static class OrderRecords
{
    public static Task<OrderBody> ReadOrderAsync(this HttpResponseMessage response) => response.ReadJsonAsync<OrderBody>();

    public static async Task<OrderPage> GetOrdersAsync(this HttpClient client, string query = "")
    {
        var response = await client.GetAsync($"/api/orders{query}");
        response.EnsureSuccessStatusCode();
        return await response.ReadJsonAsync<OrderPage>();
    }

    /// <summary>The id of one of the client's orders, found by its number.</summary>
    public static async Task<int> OrderIdAsync(this HttpClient client, string orderNumber) =>
        (await client.GetOrdersAsync("?pageSize=50")).Items.Single(order => order.OrderNumber == orderNumber).Id;

    /// <summary>The numeric part of <c>ORD-1001</c>.</summary>
    public static long NumberOf(string orderNumber) =>
        long.Parse(orderNumber["ORD-".Length..], CultureInfo.InvariantCulture);
}

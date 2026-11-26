using System.Text.Json;

namespace Store.API.Tests.Fixtures;

/// <summary>Test-local shapes of the cart responses (contracts/api.md "Cart").</summary>
public sealed record CartLine(
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
    string Status);

public sealed record CartBody(
    List<CartLine> Items,
    int ItemCount,
    decimal Subtotal,
    decimal ShippingFee,
    decimal Total,
    bool CanCheckout);

public static class CartRecords
{
    /// <summary>Seeded SKUs used by the shopping tests (001 seed data).</summary>
    public const string RibbedTopWhiteS = "WTP-009-WHT-S";     // stock 20, price 300
    public const string CottonTeeBlackM = "MTS-001-BLK-M";     // stock 1, price 350
    public const string CottonTeeWhiteL = "MTS-001-WHT-L";     // stock 0
    public const string CottonTeeBlackL = "MTS-001-BLK-L";     // ORD-0008 item
    public const string SneakersWhite43 = "SSN-013-WHT-43";    // stock 3

    public static Task<CartBody> ReadCartAsync(this HttpResponseMessage response) => response.ReadJsonAsync<CartBody>();

    public static async Task<CartBody> GetCartAsync(this HttpClient client)
    {
        var response = await client.GetAsync("/api/cart");
        response.EnsureSuccessStatusCode();
        return await response.ReadCartAsync();
    }

    /// <summary>The <c>details</c> object of an error response.</summary>
    public static async Task<JsonElement> ReadErrorDetailsAsync(this HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("details").Clone();
    }
}

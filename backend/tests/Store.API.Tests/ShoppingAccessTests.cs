using System.Net;
using System.Text;
using Shouldly;
using Store.API.Tests.Fixtures;

namespace Store.API.Tests;

/// <summary>
/// US5, AC 1 (research R2): every shopping endpoint is for customers only. No token → 401
/// <c>unauthorized</c>; an admin token → 403 <c>forbidden</c>; a customer token gets through.
/// </summary>
public class ShoppingAccessTests(StoreApiFactory factory) : IClassFixture<StoreApiFactory>
{
    private const string AddItemBody = """{ "variantId": 1, "quantity": 1 }""";
    private const string QuantityBody = """{ "quantity": 1 }""";
    private const string MergeBody = """{ "items": [] }""";
    private const string ShippingBody =
        """{ "shippingName": "Test Customer", "shippingPhone": "+201000000000", "city": "Cairo", "addressLine": "12 Tahrir Street" }""";

    public static TheoryData<string, string, string?> Endpoints => new()
    {
        { "GET", "/api/cart", null },
        { "POST", "/api/cart/items", AddItemBody },
        { "PUT", "/api/cart/items/1", QuantityBody },
        { "DELETE", "/api/cart/items/1", null },
        { "DELETE", "/api/cart", null },
        { "POST", "/api/cart/merge", MergeBody },
        { "POST", "/api/orders", ShippingBody },
        { "GET", "/api/orders", null },
        { "GET", "/api/orders/1", null },
        { "POST", "/api/orders/1/cancel", null }
    };

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task NoToken_Returns401Unauthorized(string method, string path, string? body)
    {
        using var client = factory.CreateClient();

        var response = await client.SendAsync(CreateRequest(method, path, body));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await response.ReadErrorAsync()).Code.ShouldBe("unauthorized");
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task AdminToken_Returns403Forbidden(string method, string path, string? body)
    {
        using var client = await factory.LoginAdminAsync();

        var response = await client.SendAsync(CreateRequest(method, path, body));

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var error = await response.ReadErrorAsync();
        error.Code.ShouldBe("forbidden");
        error.Message.ShouldNotBeNullOrWhiteSpace();
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task CustomerToken_IsAllowed(string method, string path, string? body)
    {
        var (client, _) = await factory.NewCustomerAsync();

        var response = await client.SendAsync(CreateRequest(method, path, body));

        // Business answers (e.g. 404 for someone else's order, 400 cartEmpty) are fine; access is not refused.
        response.StatusCode.ShouldNotBe(HttpStatusCode.Unauthorized);
        response.StatusCode.ShouldNotBe(HttpStatusCode.Forbidden);
        ((int)response.StatusCode).ShouldBeLessThan(500);
    }

    private static HttpRequestMessage CreateRequest(string method, string path, string? body) =>
        new(new HttpMethod(method), path)
        {
            Content = body is null ? null : new StringContent(body, Encoding.UTF8, "application/json")
        };
}

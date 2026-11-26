using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;

namespace Store.API.Tests.Fixtures;

/// <summary>Helpers for the 003 shopping tests: authenticated clients, variant ids and request bodies.</summary>
public static class ShoppingClient
{
    public static readonly object ValidShipping = new
    {
        shippingName = "Test Customer",
        shippingPhone = "+201000000000",
        city = "Cairo",
        addressLine = "12 Tahrir Street, Downtown",
        notes = (string?)null
    };

    /// <summary>A client whose requests carry the given access token.</summary>
    public static HttpClient CreateAuthorizedClient(this StoreApiFactory factory, string accessToken)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    /// <summary>A freshly registered customer (own cart and orders) and a client using their token.</summary>
    public static async Task<(HttpClient Client, AuthBody Auth)> NewCustomerAsync(this StoreApiFactory factory)
    {
        using var anonymous = factory.CreateClient();
        var auth = await anonymous.RegisterNewCustomerAsync();
        return (factory.CreateAuthorizedClient(auth.AccessToken), auth);
    }

    public static Task<HttpClient> LoginDemoCustomerAsync(this StoreApiFactory factory) =>
        LoginAsync(factory, "customer@store.local", StoreApiFactory.CustomerPassword);

    public static Task<HttpClient> LoginAdminAsync(this StoreApiFactory factory) =>
        LoginAsync(factory, "admin@store.local", StoreApiFactory.AdminPassword);

    public static async Task<int> VariantIdAsync(this StoreApiFactory factory, string sku)
    {
        await using var context = factory.CreateDbContext();
        return await context.ProductVariants.Where(variant => variant.Sku == sku).Select(variant => variant.Id).SingleAsync();
    }

    public static async Task<int> StockAsync(this StoreApiFactory factory, string sku)
    {
        await using var context = factory.CreateDbContext();
        return await context.ProductVariants.Where(variant => variant.Sku == sku).Select(variant => variant.Stock).SingleAsync();
    }

    public static Task<HttpResponseMessage> AddToCartAsync(this HttpClient client, int variantId, int quantity) =>
        client.PostAsJsonAsync("/api/cart/items", new { variantId, quantity });

    public static Task<HttpResponseMessage> CheckoutAsync(this HttpClient client, object? shipping = null) =>
        client.PostAsJsonAsync("/api/orders", shipping ?? ValidShipping);

    private static async Task<HttpClient> LoginAsync(StoreApiFactory factory, string email, string password)
    {
        using var anonymous = factory.CreateClient();
        var auth = await anonymous.LoginOkAsync(email, password);
        return factory.CreateAuthorizedClient(auth.AccessToken);
    }
}

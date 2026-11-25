using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;

namespace Store.API.Tests.Fixtures;

/// <summary>Test-local shapes of the auth responses (contracts/api.md "Auth").</summary>
public sealed record UserBody(int Id, string FullName, string Email, string Role);

public sealed record AuthBody(
    string AccessToken,
    DateTime AccessTokenExpiresAt,
    string RefreshToken,
    DateTime RefreshTokenExpiresAt,
    UserBody User);

public static class AuthClient
{
    public const string ValidPassword = "Valid#Pass1";

    public static string UniqueEmail(string prefix = "user") => $"{prefix}.{Guid.NewGuid():N}@store.local";

    public static object Registration(string email, string password = ValidPassword) => new
    {
        fullName = "Test Customer",
        email,
        phone = "+201000000000",
        password
    };

    public static Task<HttpResponseMessage> RegisterAsync(this HttpClient client, object body) =>
        client.PostAsJsonAsync("/api/auth/register", body);

    public static Task<HttpResponseMessage> LoginAsync(this HttpClient client, string email, string password) =>
        client.PostAsJsonAsync("/api/auth/login", new { email, password });

    public static async Task<AuthBody> RegisterNewCustomerAsync(this HttpClient client, string? email = null)
    {
        var response = await client.RegisterAsync(Registration(email ?? UniqueEmail()));
        response.EnsureSuccessStatusCode();
        return await response.ReadJsonAsync<AuthBody>();
    }

    public static Task<HttpResponseMessage> GetMeAsync(this HttpClient client, string? accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        if (accessToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        return client.SendAsync(request);
    }

    /// <summary>Same SHA-256 uppercase hex the API stores (research R4).</summary>
    public static string HashToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}

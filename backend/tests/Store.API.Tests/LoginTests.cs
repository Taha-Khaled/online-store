using System.Net;
using Shouldly;
using Store.API.Tests.Fixtures;

namespace Store.API.Tests;

/// <summary>US3-3/5/6 / AC 2, 3, 12: login with the seeded and registered accounts (FR-014, FR-015, FR-018).</summary>
public class LoginTests(StoreApiFactory factory) : IClassFixture<StoreApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Theory]
    [InlineData("customer@store.local", StoreApiFactory.CustomerPassword, "Demo Customer", "Customer")]
    [InlineData("admin@store.local", StoreApiFactory.AdminPassword, "Store Admin", "Admin")]
    public async Task SeededAccounts_LogInWithTheirSeededPasswords(string email, string password, string fullName, string role)
    {
        var response = await _client.LoginAsync(email, password);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var auth = await response.ReadJsonAsync<AuthBody>();
        auth.AccessToken.ShouldNotBeNullOrWhiteSpace();
        auth.RefreshToken.ShouldNotBeNullOrWhiteSpace();
        auth.User.Email.ShouldBe(email);
        auth.User.FullName.ShouldBe(fullName);
        auth.User.Role.ShouldBe(role);
    }

    [Fact]
    public async Task Email_IsMatchedIgnoringCaseAndSpaces()
    {
        var response = await _client.LoginAsync("  CUSTOMER@Store.Local ", StoreApiFactory.CustomerPassword);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RegisteredCustomer_CanLogIn()
    {
        var email = AuthClient.UniqueEmail();
        await _client.RegisterNewCustomerAsync(email);

        var response = await _client.LoginAsync(email, AuthClient.ValidPassword);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task WrongPasswordAndUnknownEmail_ReturnTheSameResponse()
    {
        var wrongPassword = await _client.LoginAsync("customer@store.local", "Wrong#Pass1");
        var unknownEmail = await _client.LoginAsync("nobody@store.local", "Wrong#Pass1");

        wrongPassword.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        unknownEmail.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var wrongPasswordBody = await wrongPassword.Content.ReadAsStringAsync();
        (await unknownEmail.Content.ReadAsStringAsync()).ShouldBe(wrongPasswordBody);
        (await wrongPassword.ReadErrorAsync()).Code.ShouldBe("invalidCredentials");
    }

    [Fact]
    public async Task EmptyFields_Return400ForBoth()
    {
        var response = await _client.LoginAsync("", "");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ReadErrorAsync()).Errors.ShouldNotBeNull().Keys.ShouldBe(["email", "password"], ignoreOrder: true);
    }

    [Fact]
    public async Task ShortPassword_IsStillCheckedNotRejectedByRegistrationRules()
    {
        var response = await _client.LoginAsync("customer@store.local", "x");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_NeverSetsCookies()
    {
        var response = await _client.LoginAsync("customer@store.local", StoreApiFactory.CustomerPassword);

        response.Headers.Contains("Set-Cookie").ShouldBeFalse();
    }

    [Fact]
    public async Task EachLogin_CreatesASeparateRefreshToken()
    {
        var first = await (await _client.LoginAsync("customer@store.local", StoreApiFactory.CustomerPassword)).ReadJsonAsync<AuthBody>();
        var second = await (await _client.LoginAsync("customer@store.local", StoreApiFactory.CustomerPassword)).ReadJsonAsync<AuthBody>();

        second.RefreshToken.ShouldNotBe(first.RefreshToken);
    }
}

using System.Net;
using Shouldly;
using Store.API.Tests.Fixtures;

namespace Store.API.Tests;

/// <summary>US4-3 / AC 14: logout revokes one device's refresh token only and always returns 204 (FR-024, FR-025, FR-029).</summary>
public class LogoutTests(StoreApiFactory factory) : IClassFixture<StoreApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task LogoutFirstDevice_FirstStopsWorking_SecondStillWorks()
    {
        var (first, second) = await TwoDevicesAsync();

        (await _client.LogoutAsync(first.RefreshToken)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await _client.RefreshAsync(first.RefreshToken)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await _client.RefreshAsync(second.RefreshToken)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task LogoutFirstDevice_SecondCheckedFirst_ThenFirst()
    {
        var (first, second) = await TwoDevicesAsync();

        (await _client.LogoutAsync(first.RefreshToken)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await _client.RefreshAsync(second.RefreshToken)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await _client.RefreshAsync(first.RefreshToken)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AccessToken_StaysValidAfterLogout()
    {
        var (first, _) = await TwoDevicesAsync();

        await _client.LogoutAsync(first.RefreshToken);

        (await _client.GetMeAsync(first.AccessToken)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UnknownToken_Returns204()
    {
        (await _client.LogoutAsync("unknown-token-value")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task AlreadyLoggedOutToken_Returns204()
    {
        var (first, second) = await TwoDevicesAsync();
        await _client.LogoutAsync(first.RefreshToken);

        (await _client.LogoutAsync(first.RefreshToken)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await _client.RefreshAsync(second.RefreshToken)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ExpiredToken_Returns204AndOtherTokensKeepWorking()
    {
        var expired = await _client.RegisterNewCustomerAsync();
        factory.Time.Advance(TimeSpan.FromDays(7));
        var fresh = await _client.LoginOkAsync(expired.User.Email);

        (await _client.LogoutAsync(expired.RefreshToken)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await _client.RefreshAsync(fresh.RefreshToken)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RotatedToken_Returns204_WithoutReuseDetection()
    {
        var registered = await _client.RegisterNewCustomerAsync();
        var replacement = await _client.RefreshOkAsync(registered.RefreshToken);

        (await _client.LogoutAsync(registered.RefreshToken)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await _client.RefreshAsync(replacement.RefreshToken)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task EmptyToken_Returns400()
    {
        var response = await _client.LogoutAsync("");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ReadErrorAsync()).Errors.ShouldNotBeNull().Keys.ShouldBe(["refreshToken"]);
    }

    private async Task<(AuthBody First, AuthBody Second)> TwoDevicesAsync()
    {
        var first = await _client.RegisterNewCustomerAsync();
        var second = await _client.LoginOkAsync(first.User.Email);
        return (first, second);
    }
}

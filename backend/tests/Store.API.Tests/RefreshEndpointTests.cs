using System.Net;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Store.API.Tests.Fixtures;

namespace Store.API.Tests;

/// <summary>
/// US4 / AC 1, 13, 15: refresh rotation, reuse detection, expiry and storage (FR-019–FR-023,
/// FR-028). Every test uses its own freshly registered user, because reuse detection revokes all
/// of a user's tokens.
/// </summary>
public class RefreshEndpointTests(StoreApiFactory factory) : IClassFixture<StoreApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task RegistrationRefreshToken_WorksImmediately()
    {
        var registered = await _client.RegisterNewCustomerAsync();

        var response = await _client.RefreshAsync(registered.RefreshToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Refresh_ReturnsWorkingNewPair_AndOldTokenStopsWorking()
    {
        var registered = await _client.RegisterNewCustomerAsync();

        var refreshed = await _client.RefreshOkAsync(registered.RefreshToken);

        refreshed.RefreshToken.ShouldNotBe(registered.RefreshToken);
        refreshed.AccessToken.ShouldNotBe(registered.AccessToken);
        refreshed.User.ShouldBe(registered.User);
        (await _client.GetMeAsync(refreshed.AccessToken)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var old = await _client.RefreshAsync(registered.RefreshToken);
        old.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await old.ReadErrorAsync()).Code.ShouldBe("invalidRefreshToken");
    }

    [Fact]
    public async Task NewTokenFromRefresh_CanBeRefreshedAgain()
    {
        var registered = await _client.RegisterNewCustomerAsync();
        var second = await _client.RefreshOkAsync(registered.RefreshToken);

        var third = await _client.RefreshAsync(second.RefreshToken);

        third.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ReusingARotatedToken_RevokesEveryTokenOfTheUser()
    {
        var registered = await _client.RegisterNewCustomerAsync();
        var otherDevice = await _client.LoginOkAsync(registered.User.Email);
        var rotated = await _client.RefreshOkAsync(registered.RefreshToken);

        var reuse = await _client.RefreshAsync(registered.RefreshToken);

        reuse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await _client.RefreshAsync(rotated.RefreshToken)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await _client.RefreshAsync(otherDevice.RefreshToken)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        await using var context = factory.CreateDbContext();
        (await context.RefreshTokens.Where(token => token.UserId == registered.User.Id).AllAsync(token => token.RevokedAt != null))
            .ShouldBeTrue();
    }

    [Fact]
    public async Task LoggedOutToken_Returns401_WithoutRevokingOtherTokens()
    {
        var registered = await _client.RegisterNewCustomerAsync();
        var otherDevice = await _client.LoginOkAsync(registered.User.Email);
        (await _client.LogoutAsync(registered.RefreshToken)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var loggedOut = await _client.RefreshAsync(registered.RefreshToken);

        loggedOut.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await loggedOut.ReadErrorAsync()).Code.ShouldBe("invalidRefreshToken");
        (await _client.RefreshAsync(otherDevice.RefreshToken)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Token_WorksUntilJustBefore7Days()
    {
        var registered = await _client.RegisterNewCustomerAsync();

        factory.Time.Advance(TimeSpan.FromDays(7) - TimeSpan.FromSeconds(1));

        (await _client.RefreshAsync(registered.RefreshToken)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Token_IsRejectedAt7Days()
    {
        var registered = await _client.RegisterNewCustomerAsync();

        factory.Time.Advance(TimeSpan.FromDays(7));

        var response = await _client.RefreshAsync(registered.RefreshToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await response.ReadErrorAsync()).Code.ShouldBe("invalidRefreshToken");
    }

    [Fact]
    public async Task RotatedTokenUsedAfterItExpired_StillTriggersReuseDetection()
    {
        var registered = await _client.RegisterNewCustomerAsync();
        var otherDevice = await _client.LoginOkAsync(registered.User.Email);
        await _client.RefreshOkAsync(registered.RefreshToken);
        factory.Time.Advance(TimeSpan.FromDays(8));

        (await _client.RefreshAsync(registered.RefreshToken)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // The other device's token was expired but not revoked; reuse detection revokes it too (FR-023).
        await using var context = factory.CreateDbContext();
        var hash = AuthClient.HashToken(otherDevice.RefreshToken);
        (await context.RefreshTokens.SingleAsync(token => token.TokenHash == hash)).RevokedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task UnknownAndMalformedTokens_ReturnTheSame401()
    {
        var registered = await _client.RegisterNewCustomerAsync();
        await _client.RefreshOkAsync(registered.RefreshToken);

        var rotated = await (await _client.RefreshAsync(registered.RefreshToken)).Content.ReadAsStringAsync();
        var unknown = await (await _client.RefreshAsync("unknown-token-value")).Content.ReadAsStringAsync();
        var malformed = await (await _client.RefreshAsync("%%%not base64%%%")).Content.ReadAsStringAsync();

        unknown.ShouldBe(rotated);
        malformed.ShouldBe(rotated);
    }

    [Fact]
    public async Task EmptyToken_Returns400()
    {
        var response = await _client.RefreshAsync("");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ReadErrorAsync()).Errors.ShouldNotBeNull().Keys.ShouldBe(["refreshToken"]);
    }

    [Fact]
    public async Task Database_NeverStoresARefreshTokenInPlainText()
    {
        var registered = await _client.RegisterNewCustomerAsync();
        var login = await _client.LoginOkAsync(registered.User.Email);
        var refreshed = await _client.RefreshOkAsync(login.RefreshToken);
        string[] issued = [registered.RefreshToken, login.RefreshToken, refreshed.RefreshToken];

        await using var context = factory.CreateDbContext();
        var rows = await context.RefreshTokens.Where(token => token.UserId == registered.User.Id).ToListAsync();

        rows.Count.ShouldBe(3);
        rows.ShouldAllBe(row => row.TokenHash.Length == 64 && row.TokenHash.All(Uri.IsHexDigit));
        foreach (var row in rows)
        {
            issued.ShouldNotContain(row.TokenHash);
            issued.ShouldNotContain(row.ReplacedByTokenHash ?? string.Empty);
        }
    }
}

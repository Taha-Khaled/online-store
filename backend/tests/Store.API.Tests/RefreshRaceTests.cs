using System.Net;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Store.API.Tests.Fixtures;

namespace Store.API.Tests;

/// <summary>
/// Research R4 / SC-006: parallel refreshes with one token. The concurrency token on RevokedAt lets
/// exactly one rotation succeed. Every other request sees a rotated token, which is a reuse, so the
/// winner's new token is revoked too. This is the intended strict behavior: the frontend must run
/// only one refresh at a time per session.
/// </summary>
public class RefreshRaceTests(StoreApiFactory factory) : IClassFixture<StoreApiFactory>
{
    private const int ParallelRequests = 10;

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task ParallelRefreshes_ExactlyOneSucceeds_AndTheLosersRevokeTheWinnersNewToken()
    {
        var registered = await _client.RegisterNewCustomerAsync();

        var responses = await Task.WhenAll(
            Enumerable.Range(0, ParallelRequests).Select(_ => _client.RefreshAsync(registered.RefreshToken)));

        responses.Count(response => response.StatusCode == HttpStatusCode.OK).ShouldBe(1);
        responses.Count(response => response.StatusCode == HttpStatusCode.Unauthorized).ShouldBe(ParallelRequests - 1);

        var winner = await responses.Single(response => response.StatusCode == HttpStatusCode.OK).ReadJsonAsync<AuthBody>();

        // Intended: the losers presented a rotated token, so reuse detection revoked the winner's new token.
        var winnersNext = await _client.RefreshAsync(winner.RefreshToken);
        winnersNext.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await winnersNext.ReadErrorAsync()).Code.ShouldBe("invalidRefreshToken");

        await using var context = factory.CreateDbContext();
        var winnerHash = AuthClient.HashToken(winner.RefreshToken);
        (await context.RefreshTokens.SingleAsync(token => token.TokenHash == winnerHash)).RevokedAt.ShouldNotBeNull();
        (await context.RefreshTokens.CountAsync(token => token.UserId == registered.User.Id)).ShouldBe(2);
    }
}

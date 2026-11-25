using System.Net;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Shouldly;
using Store.API.Tests.Fixtures;

namespace Store.API.Tests;

/// <summary>US3-3/4 / AC 2, 19: the current-user endpoint and access token validation (FR-016–FR-018).</summary>
public class MeTests(StoreApiFactory factory) : IClassFixture<StoreApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task ValidToken_ReturnsDemoCustomer()
    {
        var auth = await LoginCustomerAsync();

        var response = await _client.GetMeAsync(auth.AccessToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var me = await response.ReadJsonAsync<UserBody>();
        me.FullName.ShouldBe("Demo Customer");
        me.Email.ShouldBe("customer@store.local");
        me.Role.ShouldBe("Customer");
        response.Headers.Contains("Set-Cookie").ShouldBeFalse();
    }

    [Fact]
    public async Task NoToken_Returns401WithErrorShape()
    {
        var response = await _client.GetMeAsync(null);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await response.ReadErrorAsync()).Code.ShouldBe("unauthorized");
    }

    [Fact]
    public async Task GarbageToken_Returns401()
    {
        var response = await _client.GetMeAsync("not-a-jwt");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await response.ReadErrorAsync()).Code.ShouldBe("unauthorized");
    }

    [Theory]
    [InlineData("another-signing-key-0123456789-abcdefghij", StoreApiFactory.JwtIssuer, StoreApiFactory.JwtAudience)]
    [InlineData(StoreApiFactory.JwtKey, "someone-else", StoreApiFactory.JwtAudience)]
    [InlineData(StoreApiFactory.JwtKey, StoreApiFactory.JwtIssuer, "someone-else")]
    public async Task TokenWithWrongKeyIssuerOrAudience_Returns401(string key, string issuer, string audience)
    {
        var token = CreateToken(key, issuer, audience);

        var response = await _client.GetMeAsync(token);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task HandMadeTokenWithTheRightSettings_IsAccepted()
    {
        // Guards the theory above: the hand-made token is valid when key, issuer and audience match.
        var response = await _client.GetMeAsync(CreateToken(StoreApiFactory.JwtKey, StoreApiFactory.JwtIssuer, StoreApiFactory.JwtAudience));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Token_IsAcceptedUntil15MinutesOld_ThenRejectedWithoutGrace()
    {
        var auth = await LoginCustomerAsync();

        factory.Time.Advance(TimeSpan.FromMinutes(15) - TimeSpan.FromSeconds(1));
        (await _client.GetMeAsync(auth.AccessToken)).StatusCode.ShouldBe(HttpStatusCode.OK);

        factory.Time.Advance(TimeSpan.FromSeconds(1));
        var expired = await _client.GetMeAsync(auth.AccessToken);
        expired.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await expired.ReadErrorAsync()).Code.ShouldBe("unauthorized");
    }

    [Fact]
    public async Task DeletedUser_Returns401()
    {
        var auth = await _client.RegisterNewCustomerAsync();
        await using (var context = factory.CreateDbContext())
        {
            await context.Users.Where(user => user.Id == auth.User.Id).ExecuteDeleteAsync();
        }

        var response = await _client.GetMeAsync(auth.AccessToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await response.ReadErrorAsync()).Code.ShouldBe("unauthorized");
    }

    private async Task<AuthBody> LoginCustomerAsync()
    {
        var response = await _client.LoginAsync("customer@store.local", StoreApiFactory.CustomerPassword);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await response.ReadJsonAsync<AuthBody>();
    }

    private string CreateToken(string key, string issuer, string audience)
    {
        var now = factory.Time.GetUtcNow().UtcDateTime;
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = now.AddMinutes(15),
            Subject = new ClaimsIdentity([new Claim("sub", "2"), new Claim("role", "Customer")]),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                SecurityAlgorithms.HmacSha256)
        });
    }
}

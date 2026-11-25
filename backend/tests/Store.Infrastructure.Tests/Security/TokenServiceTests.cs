using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Shouldly;
using Store.Domain.Enums;
using Store.Infrastructure.Security;
using Store.Infrastructure.Tests.Fixtures;

namespace Store.Infrastructure.Tests.Security;

/// <summary>Research R2 / R4: access token contents and refresh token generation and hashing.</summary>
public class TokenServiceTests
{
    private const string Key = "unit-test-signing-key-0123456789-abcdef";
    private const string Issuer = "store-api";
    private const string Audience = "store-web";

    private static readonly DateTimeOffset Now = new(2026, 11, 24, 12, 0, 0, TimeSpan.Zero);

    private readonly TokenService _service = new(
        Options.Create(new JwtOptions { Key = Key, Issuer = Issuer, Audience = Audience }),
        new FixedTimeProvider(Now));

    [Fact]
    public async Task AccessToken_IsSignedAndCarriesUserClaims()
    {
        var issued = _service.CreateAccessToken(7, "customer@store.local", Role.Customer);

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(issued.Token, new TokenValidationParameters
        {
            ValidIssuer = Issuer,
            ValidAudience = Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key)),
            ValidateLifetime = false
        });

        result.IsValid.ShouldBeTrue(result.Exception?.Message);
        var token = (JsonWebToken)result.SecurityToken;
        token.Subject.ShouldBe("7");
        token.GetClaim("email").Value.ShouldBe("customer@store.local");
        token.GetClaim("role").Value.ShouldBe("Customer");
        token.Id.ShouldNotBeNullOrWhiteSpace();
        token.Alg.ShouldBe(SecurityAlgorithms.HmacSha256);
    }

    [Fact]
    public void AccessToken_ExpiresExactly15MinutesAfterIssue()
    {
        var issued = _service.CreateAccessToken(7, "customer@store.local", Role.Admin);

        var token = new JsonWebToken(issued.Token);
        token.IssuedAt.ShouldBe(Now.UtcDateTime);
        token.ValidFrom.ShouldBe(Now.UtcDateTime);
        token.ValidTo.ShouldBe(Now.UtcDateTime.AddMinutes(15));
        issued.ExpiresAt.ShouldBe(Now.UtcDateTime.AddMinutes(15));
        token.GetClaim("role").Value.ShouldBe("Admin");
    }

    [Fact]
    public async Task AccessToken_SignedWithAnotherKey_DoesNotValidate()
    {
        var issued = _service.CreateAccessToken(7, "customer@store.local", Role.Customer);

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(issued.Token, new TokenValidationParameters
        {
            ValidIssuer = Issuer,
            ValidAudience = Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("another-signing-key-0123456789-abcdef")),
            ValidateLifetime = false
        });

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void RefreshToken_IsRandomUrlSafeAndValidFor7Days()
    {
        var first = _service.CreateRefreshToken();
        var second = _service.CreateRefreshToken();

        first.Token.ShouldNotBe(second.Token);
        first.Token.Length.ShouldBe(86);
        first.Token.ShouldMatch("^[A-Za-z0-9_-]+$");
        first.CreatedAt.ShouldBe(Now.UtcDateTime);
        first.ExpiresAt.ShouldBe(Now.UtcDateTime.AddDays(7));
    }

    [Fact]
    public void RefreshTokenHash_Is64HexCharacters_DeterministicAndNotTheToken()
    {
        var issued = _service.CreateRefreshToken();

        issued.Hash.ShouldMatch("^[0-9A-F]{64}$");
        issued.Hash.ShouldNotBe(issued.Token);
        _service.HashRefreshToken(issued.Token).ShouldBe(issued.Hash);
        _service.HashRefreshToken(issued.Token + "x").ShouldNotBe(issued.Hash);
    }
}

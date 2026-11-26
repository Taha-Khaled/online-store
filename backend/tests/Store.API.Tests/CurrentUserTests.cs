using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using Shouldly;
using Store.API.Authorization;
using Store.Application.Common.Errors;

namespace Store.API.Tests;

/// <summary>The user id comes only from a numeric <c>sub</c> claim; anything else is 401 <c>unauthorized</c>.</summary>
public class CurrentUserTests
{
    [Fact]
    public void NumericSubClaim_IsTheUserId()
    {
        var user = Principal(new Claim(JwtRegisteredClaimNames.Sub, "42"));

        user.GetUserId().ShouldBe(42);
    }

    [Fact]
    public void MissingSubClaim_ThrowsUnauthorized()
    {
        var error = Should.Throw<UnauthorizedException>(() => Principal().GetUserId());

        error.Code.ShouldBe(ErrorCodes.Unauthorized);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("-5")]
    [InlineData(" 7")]
    public void NonNumericSubClaim_ThrowsUnauthorized(string value)
    {
        var user = Principal(new Claim(JwtRegisteredClaimNames.Sub, value));

        Should.Throw<UnauthorizedException>(() => user.GetUserId()).Code.ShouldBe(ErrorCodes.Unauthorized);
    }

    private static ClaimsPrincipal Principal(params Claim[] claims) => new(new ClaimsIdentity(claims, "test"));
}

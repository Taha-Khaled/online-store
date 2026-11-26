using System.Globalization;
using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using Store.Application.Common.Errors;

namespace Store.API.Authorization;

public static class CurrentUser
{
    /// <summary>The user id from the access token's <c>sub</c> claim; 401 when it is missing or invalid.</summary>
    public static int GetUserId(this ClaimsPrincipal user) =>
        int.TryParse(user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var userId)
            ? userId
            : throw new UnauthorizedException(ErrorCodes.Unauthorized, "Authentication is required.");
}

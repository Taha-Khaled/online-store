using Store.Application.Auth.Responses;
using Store.Application.Common.Security;
using Store.Domain.Enums;

namespace Store.Application.Auth.Services;

/// <summary>Builds the login/registration/refresh body from a new refresh token and a fresh access token.</summary>
internal static class AuthResponses
{
    public static AuthResponse Create(
        ITokenService tokenService,
        int userId,
        string fullName,
        string email,
        Role role,
        IssuedRefreshToken refreshToken)
    {
        var accessToken = tokenService.CreateAccessToken(userId, email, role);
        return new AuthResponse(
            accessToken.Token,
            accessToken.ExpiresAt,
            refreshToken.Token,
            refreshToken.ExpiresAt,
            new UserResponse(userId, fullName, email, role.ToString()));
    }
}

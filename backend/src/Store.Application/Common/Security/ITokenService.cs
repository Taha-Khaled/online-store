using Store.Domain.Enums;

namespace Store.Application.Common.Security;

/// <summary>Issues access tokens and refresh tokens (research R2, R4).</summary>
public interface ITokenService
{
    /// <summary>A signed JWT for the user, valid for 15 minutes from now.</summary>
    IssuedAccessToken CreateAccessToken(int userId, string email, Role role);

    /// <summary>A new random refresh token, its hash, and its validity window (7 days from now).</summary>
    IssuedRefreshToken CreateRefreshToken();

    /// <summary>The stored form of a refresh token presented by a client.</summary>
    string HashRefreshToken(string refreshToken);
}

public sealed record IssuedAccessToken(string Token, DateTime ExpiresAt);

public sealed record IssuedRefreshToken(string Token, string Hash, DateTime CreatedAt, DateTime ExpiresAt);

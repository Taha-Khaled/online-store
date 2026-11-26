namespace Store.Application.Auth.Responses;

/// <summary>Login, registration and refresh result (contracts/api.md "Auth").</summary>
public sealed record AuthResponse(
    string AccessToken,
    DateTime AccessTokenExpiresAt,
    string RefreshToken,
    DateTime RefreshTokenExpiresAt,
    UserResponse User);

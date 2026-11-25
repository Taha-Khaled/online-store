using System.Buffers.Text;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Store.Application.Common.Security;
using Store.Domain.Enums;

namespace Store.Infrastructure.Security;

/// <summary>
/// JWT access tokens (HMAC-SHA256, claims sub/email/role/jti) and random refresh tokens stored as
/// SHA-256 hashes. A fast hash is right here: the token is 512 random bits, unlike a password
/// (research R2, R4). All times come from <see cref="TimeProvider"/>.
/// </summary>
public sealed class TokenService(IOptions<JwtOptions> options, TimeProvider timeProvider) : ITokenService
{
    public const string RoleClaim = "role";

    private const int RefreshTokenBytes = 64;

    private static readonly JsonWebTokenHandler Handler = new();

    public IssuedAccessToken CreateAccessToken(int userId, string email, Role role)
    {
        var jwt = options.Value;
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var expiresAt = now.Add(JwtOptions.AccessTokenLifetime);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = expiresAt,
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                new Claim(JwtRegisteredClaimNames.Email, email),
                new Claim(RoleClaim, role.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
            ]),
            SigningCredentials = new SigningCredentials(jwt.CreateSigningKey(), SecurityAlgorithms.HmacSha256)
        };

        return new IssuedAccessToken(Handler.CreateToken(descriptor), expiresAt);
    }

    public IssuedRefreshToken CreateRefreshToken()
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var token = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(RefreshTokenBytes));
        return new IssuedRefreshToken(token, HashRefreshToken(token), now, now.Add(JwtOptions.RefreshTokenLifetime));
    }

    public string HashRefreshToken(string refreshToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));
}

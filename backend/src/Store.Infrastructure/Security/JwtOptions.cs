using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Store.Infrastructure.Security;

/// <summary>
/// The "Jwt" configuration section (research R3). Values come from User Secrets locally and
/// environment variables in production; never from committed files.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public const int MinimumKeyBytes = 32;

    public static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(7);

    public string? Key { get; set; }

    public string? Issuer { get; set; }

    public string? Audience { get; set; }

    public SymmetricSecurityKey CreateSigningKey() =>
        new(Encoding.UTF8.GetBytes(Key ?? throw new InvalidOperationException("'Jwt:Key' is not configured.")));
}

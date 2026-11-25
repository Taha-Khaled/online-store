using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Store.API.Errors;
using Store.Infrastructure.Security;

namespace Store.API.Extensions;

public static class JwtAuthenticationExtensions
{
    /// <summary>
    /// Bearer-only JWT authentication (FR-017, FR-018, research R2): issuer, audience, signing key
    /// and lifetime are validated with no clock skew; lifetime uses the injected
    /// <see cref="TimeProvider"/>, so a token is rejected the moment it is 15 minutes old. 401 and
    /// 403 use the error shape (research R6).
    /// </summary>
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services)
    {
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        services
            .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>, TimeProvider>((bearer, jwt, timeProvider) =>
            {
                bearer.MapInboundClaims = false;
                bearer.TokenValidationParameters = CreateValidationParameters(jwt.Value, timeProvider);
                bearer.Events = new JwtBearerEvents
                {
                    OnChallenge = WriteUnauthorizedAsync,
                    OnForbidden = WriteForbiddenAsync
                };
            });

        services.AddAuthorization();
        return services;
    }

    private static TokenValidationParameters CreateValidationParameters(JwtOptions jwt, TimeProvider timeProvider) => new()
    {
        ValidateIssuer = true,
        ValidIssuer = jwt.Issuer,
        ValidateAudience = true,
        ValidAudience = jwt.Audience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = jwt.CreateSigningKey(),
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        ValidateLifetime = true,
        RequireExpirationTime = true,
        ClockSkew = TimeSpan.Zero,
        LifetimeValidator = (notBefore, expires, _, _) => IsWithinLifetime(notBefore, expires, timeProvider),
        NameClaimType = JwtRegisteredClaimNames.Sub,
        RoleClaimType = TokenService.RoleClaim
    };

    private static bool IsWithinLifetime(DateTime? notBefore, DateTime? expires, TimeProvider timeProvider)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        return expires.HasValue && now < expires.Value && (!notBefore.HasValue || notBefore.Value <= now);
    }

    private static Task WriteUnauthorizedAsync(JwtBearerChallengeContext context)
    {
        context.HandleResponse();
        return ErrorResponseWriter.WriteAsync(
            context.HttpContext,
            StatusCodes.Status401Unauthorized,
            ErrorResponseWriter.ForStatus(StatusCodes.Status401Unauthorized));
    }

    private static Task WriteForbiddenAsync(ForbiddenContext context) =>
        ErrorResponseWriter.WriteAsync(
            context.HttpContext,
            StatusCodes.Status403Forbidden,
            ErrorResponseWriter.ForStatus(StatusCodes.Status403Forbidden));
}

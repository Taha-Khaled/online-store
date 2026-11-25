using Store.Domain.Enums;

namespace Store.API.Authorization;

/// <summary>Authorization policies (003 research R2).</summary>
public static class Policies
{
    /// <summary>Logged-in users with role Customer only: no token → 401, other roles → 403.</summary>
    public const string CustomerOnly = "CustomerOnly";

    public static IServiceCollection AddStorePolicies(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            .AddPolicy(CustomerOnly, policy => policy.RequireAuthenticatedUser().RequireRole(nameof(Role.Customer)));

        return services;
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Store.Application.Common;
using Store.Application.Common.Security;
using Store.Infrastructure.Security;
using Store.Infrastructure.Persistence;
using Store.Infrastructure.Seeding;

namespace Store.Infrastructure;

public static class DependencyInjection
{
    private const string ConnectionStringName = "Store";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException(
                $"Connection string 'ConnectionStrings:{ConnectionStringName}' is not configured.");

        services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connectionString));
        services.AddScoped<IAppDbContext>(provider => provider.GetRequiredService<AppDbContext>());
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IPasswordHasher, PasswordHasher>();

        services.AddOptions<JwtOptions>().Bind(configuration.GetSection(JwtOptions.SectionName)).ValidateOnStart();
        services.AddSingleton<IValidateOptions<JwtOptions>, JwtOptionsValidator>();
        services.AddSingleton<ITokenService, TokenService>();

        services.AddOptions<ShopOptions>().Bind(configuration.GetSection(ShopOptions.SectionName)).ValidateOnStart();
        services.AddSingleton<IValidateOptions<ShopOptions>, ShopOptionsValidator>();

        services.AddOptions<SeedOptions>().Bind(configuration.GetSection(SeedOptions.SectionName));
        services.AddScoped<AccountSeeder>();
        services.AddScoped<CatalogSeeder>();
        services.AddScoped<DatabaseSeeder>();
        services.AddScoped<DatabaseInitializer>();

        return services;
    }
}

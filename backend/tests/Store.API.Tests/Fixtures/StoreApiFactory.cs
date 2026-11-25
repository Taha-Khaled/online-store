using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Store.Infrastructure.Persistence;

namespace Store.API.Tests.Fixtures;

/// <summary>
/// Starts the real API in Development against its own <c>StoreTests_&lt;guid&gt;</c> database on the
/// server from <c>STORE_TEST_SQLSERVER</c>. The factory does not create the database: the app's
/// startup migration creates and seeds it. The database is dropped on dispose (research R13).
/// </summary>
public class StoreApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string ServerVariable = "STORE_TEST_SQLSERVER";
    public const string AdminPassword = "Admin#Test-Only-1";
    public const string CustomerPassword = "Customer#Test-Only-1";
    public const string JwtKey = "test-only-signing-key-0123456789-abcdefghij";
    public const string JwtIssuer = "store-api-tests";
    public const string JwtAudience = "store-web-tests";

    public StoreApiFactory()
    {
        var server = Environment.GetEnvironmentVariable(ServerVariable);
        if (string.IsNullOrWhiteSpace(server))
        {
            throw new InvalidOperationException(
                $"Environment variable '{ServerVariable}' is not set. Set it to a SQL Server connection string without a database name.");
        }

        DatabaseName = $"StoreTests_{Guid.NewGuid():N}";
        ConnectionString = new SqlConnectionStringBuilder(server) { InitialCatalog = DatabaseName }.ConnectionString;
    }

    public string DatabaseName { get; }

    public string ConnectionString { get; }

    public MutableTimeProvider Time { get; } = new();

    public AppDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(ConnectionString).Options);

    public Task InitializeAsync() => Task.CompletedTask;

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await using var context = CreateDbContext();
        await context.Database.EnsureDeletedAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(EnvironmentName);
        builder.UseSetting("ConnectionStrings:Store", ConnectionString);
        builder.UseSetting("Seed:AdminPassword", AdminPassword);
        builder.UseSetting("Seed:CustomerPassword", CustomerPassword);
        builder.UseSetting("Jwt:Key", JwtKey);
        builder.UseSetting("Jwt:Issuer", JwtIssuer);
        builder.UseSetting("Jwt:Audience", JwtAudience);

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Time);
        });
    }

    /// <summary>Hosting environment; derived factories may run the app outside Development.</summary>
    protected virtual string EnvironmentName => "Development";
}

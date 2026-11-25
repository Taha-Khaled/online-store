using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Store.API.Tests.Fixtures;

namespace Store.API.Tests;

/// <summary>
/// US5-1/2: the API refuses to start with missing or weak JWT settings or a negative shipping fee,
/// before touching the database (002 FR-009, 003 research R3), and Swagger is available in
/// Development only (FR-006).
/// </summary>
public class StartupTests
{
    [Theory]
    [InlineData("Jwt:Key", "", "'Jwt:Key' is not configured.")]
    [InlineData("Jwt:Key", "0123456789abcdef0123456789abcde", "'Jwt:Key' must be at least 32 bytes (UTF-8).")]
    [InlineData("Jwt:Issuer", "", "'Jwt:Issuer' is not configured.")]
    [InlineData("Jwt:Audience", "", "'Jwt:Audience' is not configured.")]
    [InlineData("Shop:ShippingFee", "-1", "'Shop:ShippingFee' must be 0 or more.")]
    public async Task InvalidRequiredSetting_StopsStartupNamingTheKey_BeforeCreatingTheDatabase(string key, string value, string message)
    {
        var factory = new OverriddenSettingFactory(key, value);
        try
        {
            var error = Should.Throw<Exception>(() => factory.CreateClient());

            error.ToString().ShouldContain(message);
            await using var context = factory.CreateDbContext();
            (await context.Database.CanConnectAsync()).ShouldBeFalse("startup must stop before migrating the database");
        }
        finally
        {
            await ((IAsyncLifetime)factory).DisposeAsync();
        }
    }

    [Fact]
    public async Task Development_ServesSwaggerWithBearerSchemeOnAuthorizedActions()
    {
        var factory = new StoreApiFactory();
        try
        {
            using var client = factory.CreateClient();

            var response = await client.GetAsync("/swagger/v1/swagger.json");

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = document.RootElement;
            var scheme = root.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer");
            scheme.GetProperty("scheme").GetString().ShouldBe("bearer");
            scheme.GetProperty("bearerFormat").GetString().ShouldBe("JWT");

            var paths = root.GetProperty("paths");
            paths.GetProperty("/api/auth/me").GetProperty("get").TryGetProperty("security", out _).ShouldBeTrue();
            paths.GetProperty("/api/auth/login").GetProperty("post").TryGetProperty("security", out _).ShouldBeFalse();
            paths.TryGetProperty("/api/products", out _).ShouldBeTrue();
        }
        finally
        {
            await ((IAsyncLifetime)factory).DisposeAsync();
        }
    }

    [Fact]
    public async Task Production_DoesNotServeSwagger()
    {
        var factory = new ProductionFactory();
        try
        {
            // Production never migrates at startup; prepare the schema the way an approved deployment would.
            await using (var context = factory.CreateDbContext())
            {
                await context.Database.MigrateAsync();
            }

            using var client = factory.CreateClient();

            var response = await client.GetAsync("/swagger/v1/swagger.json");

            response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
            (await response.ReadErrorAsync()).Code.ShouldBe("notFound");
            (await client.GetAsync("/api/categories")).StatusCode.ShouldBe(HttpStatusCode.OK);
        }
        finally
        {
            await ((IAsyncLifetime)factory).DisposeAsync();
        }
    }

    private sealed class OverriddenSettingFactory(string key, string value) : StoreApiFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting(key, value);
        }
    }

    private sealed class ProductionFactory : StoreApiFactory
    {
        protected override string EnvironmentName => "Production";
    }
}

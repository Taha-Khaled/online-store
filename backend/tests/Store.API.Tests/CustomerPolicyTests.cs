using System.Net;
using Shouldly;
using Store.API.Tests.Fixtures;

namespace Store.API.Tests;

/// <summary>Research R2: the CustomerOnly policy on a test-only action (shopping endpoints: ShoppingAccessTests).</summary>
public class CustomerPolicyTests(ErrorTestApiFactory factory) : IClassFixture<ErrorTestApiFactory>
{
    private const string Path = "/api/test-errors/customer-only";

    [Fact]
    public async Task NoToken_Returns401()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync(Path);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await response.ReadErrorAsync()).Code.ShouldBe("unauthorized");
    }

    [Fact]
    public async Task AdminToken_Returns403Forbidden()
    {
        using var client = await factory.LoginAdminAsync();

        var response = await client.GetAsync(Path);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var error = await response.ReadErrorAsync();
        error.Code.ShouldBe("forbidden");
        error.Message.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task CustomerToken_IsAllowed()
    {
        using var client = await factory.LoginDemoCustomerAsync();

        var response = await client.GetAsync(Path);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}

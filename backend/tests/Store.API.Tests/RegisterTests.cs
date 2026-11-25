using System.Net;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Store.API.Tests.Fixtures;
using Store.Domain.Enums;

namespace Store.API.Tests;

/// <summary>US3-1/2/7 / AC 1, 10: customer registration (FR-010–FR-013, FR-021).</summary>
public class RegisterTests(StoreApiFactory factory) : IClassFixture<StoreApiFactory>
{
    private const string EmailDomain = "@store.local";

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task ValidRegistration_Returns201WithWorkingTokens()
    {
        var email = AuthClient.UniqueEmail("New.User").ToUpperInvariant();

        var response = await _client.RegisterAsync(AuthClient.Registration(email));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.Location!.OriginalString.ShouldBe("/api/auth/me");
        var auth = await response.ReadJsonAsync<AuthBody>();
        auth.User.Role.ShouldBe("Customer");
        auth.User.Email.ShouldBe(email.ToLowerInvariant());
        auth.User.FullName.ShouldBe("Test Customer");
        auth.AccessToken.ShouldNotBeNullOrWhiteSpace();
        auth.RefreshToken.ShouldNotBeNullOrWhiteSpace();
        auth.AccessTokenExpiresAt.ShouldBe(factory.Time.GetUtcNow().UtcDateTime.AddMinutes(15));
        auth.RefreshTokenExpiresAt.ShouldBe(factory.Time.GetUtcNow().UtcDateTime.AddDays(7));

        var me = await _client.GetMeAsync(auth.AccessToken);
        me.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await me.ReadJsonAsync<UserBody>()).ShouldBe(auth.User);
    }

    [Fact]
    public async Task ValidRegistration_StoresCustomerWithLowercaseEmailAndHashedSecrets()
    {
        var email = AuthClient.UniqueEmail("Mixed.Case");
        var auth = await _client.RegisterNewCustomerAsync(email.ToUpperInvariant());

        await using var context = factory.CreateDbContext();
        var user = await context.Users.SingleAsync(candidate => candidate.Id == auth.User.Id);
        user.Email.ShouldBe(email.ToLowerInvariant());
        user.Role.ShouldBe(Role.Customer);
        user.PasswordHash.ShouldNotContain(AuthClient.ValidPassword);

        var token = await context.RefreshTokens.SingleAsync(candidate => candidate.UserId == user.Id);
        token.TokenHash.ShouldBe(AuthClient.HashToken(auth.RefreshToken));
        token.TokenHash.ShouldNotBe(auth.RefreshToken);
    }

    [Fact]
    public async Task SameEmailInAnotherCase_Returns409EmailAlreadyExists()
    {
        var email = AuthClient.UniqueEmail("dup");
        await _client.RegisterNewCustomerAsync(email);

        var response = await _client.RegisterAsync(AuthClient.Registration(email.ToUpperInvariant()));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.ReadErrorAsync()).Code.ShouldBe("emailAlreadyExists");
    }

    [Fact]
    public async Task SeededEmail_Returns409()
    {
        var response = await _client.RegisterAsync(AuthClient.Registration("Customer@Store.Local"));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task EmptyEmail_Returns400WithEmailFieldError()
    {
        var response = await _client.RegisterAsync(AuthClient.Registration(""));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var error = await response.ReadErrorAsync();
        error.Code.ShouldBe("validationFailed");
        error.Errors.ShouldNotBeNull().Keys.ShouldBe(["email"]);
    }

    [Fact]
    public async Task EmptyBodyObject_ReportsEveryField()
    {
        var response = await _client.RegisterAsync(new { });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ReadErrorAsync()).Errors.ShouldNotBeNull().Keys
            .ShouldBe(["fullName", "email", "phone", "password"], ignoreOrder: true);
    }

    [Theory]
    [InlineData("fullName", "A", false)]
    [InlineData("fullName", "Ab", true)]
    [InlineData("fullName", "  Ab  ", true)]
    [InlineData("email", "not-an-email", false)]
    [InlineData("phone", "+201234567890", true)]
    [InlineData("phone", "01234567890", true)]
    [InlineData("phone", "+", false)]
    [InlineData("phone", "0123-456", false)]
    [InlineData("phone", "12+34", false)]
    [InlineData("password", "abcdefg1", true)]
    [InlineData("password", "abcdef1", false)]
    [InlineData("password", "abcdefgh", false)]
    [InlineData("password", "12345678", false)]
    public async Task FieldRules_ForGivenValues(string field, string value, bool valid)
    {
        await AssertRegistrationAsync(field, value, valid);
    }

    [Theory]
    [InlineData("fullName", 100, true)]
    [InlineData("fullName", 101, false)]
    [InlineData("email", 256, true)]
    [InlineData("email", 257, false)]
    [InlineData("phone", 20, true)]
    [InlineData("phone", 21, false)]
    public async Task FieldRules_AtLengthLimits(string field, int length, bool valid)
    {
        var value = field switch
        {
            "email" => Guid.NewGuid().ToString("N") + new string('a', length - 32 - EmailDomain.Length) + EmailDomain,
            "phone" => new string('1', length),
            _ => new string('a', length)
        };

        await AssertRegistrationAsync(field, value, valid);
    }

    [Fact]
    public async Task InvalidRequest_NeverEchoesThePassword()
    {
        var response = await _client.RegisterAsync(new
        {
            fullName = "A",
            email = AuthClient.UniqueEmail(),
            phone = "+201000000000",
            password = "Secret#Value9"
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).ShouldNotContain("Secret#Value9");
    }

    private async Task AssertRegistrationAsync(string field, string value, bool valid)
    {
        var response = await _client.RegisterAsync(Body(field, value));

        if (valid)
        {
            response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
            return;
        }

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ReadErrorAsync()).Errors.ShouldNotBeNull().Keys.ShouldBe([field]);
    }

    private static object Body(string field, string value) => new
    {
        fullName = field == "fullName" ? value : "Test Customer",
        email = field == "email" ? value : AuthClient.UniqueEmail(),
        phone = field == "phone" ? value : "+201000000000",
        password = field == "password" ? value : AuthClient.ValidPassword
    };
}

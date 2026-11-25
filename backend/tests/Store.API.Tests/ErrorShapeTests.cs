using System.Net;
using System.Text;
using Shouldly;
using Store.API.Tests.Fixtures;

namespace Store.API.Tests;

/// <summary>
/// Every failure uses the single error shape (FR-001–FR-003, SC-004, US5-4), including framework
/// errors: model binding, unknown routes, wrong methods and wrong media types.
/// </summary>
public class ErrorShapeTests(ErrorTestApiFactory factory) : IClassFixture<ErrorTestApiFactory>
{
    private const string Base = "/api/test-errors";

    private readonly HttpClient _client = factory.CreateClient();

    [Theory]
    [InlineData("unauthorized", HttpStatusCode.Unauthorized, "invalidCredentials", "Invalid email or password.")]
    [InlineData("not-found", HttpStatusCode.NotFound, "productNotFound", "Product not found.")]
    [InlineData("conflict", HttpStatusCode.Conflict, "emailAlreadyExists", "Email already exists.")]
    public async Task ApplicationExceptions_MapToStatusCodeAndShape(string path, HttpStatusCode status, string code, string message)
    {
        var response = await _client.GetAsync($"{Base}/{path}");

        response.StatusCode.ShouldBe(status);
        var error = await response.ReadErrorAsync();
        error.Code.ShouldBe(code);
        error.Message.ShouldBe(message);
        error.Errors.ShouldBeNull();
        (await response.Content.ReadAsStringAsync()).ShouldNotContain("\"errors\"");
    }

    [Fact]
    public async Task ValidationException_Returns400WithFieldErrors()
    {
        var response = await _client.GetAsync($"{Base}/validation");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var error = await response.ReadErrorAsync();
        error.Code.ShouldBe("validationFailed");
        error.Message.ShouldBe("One or more fields are invalid.");
        error.Errors.ShouldNotBeNull()["email"].ShouldBe(["Email is required."]);
    }

    [Fact]
    public async Task FluentValidationFailures_ReturnCamelCaseFieldErrors()
    {
        var response = await _client.GetAsync($"{Base}/validator?count=9");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var errors = (await response.ReadErrorAsync()).Errors.ShouldNotBeNull();
        errors.Keys.ShouldBe(["displayName", "count"], ignoreOrder: true);
    }

    [Fact]
    public async Task FluentValidationSuccess_ReturnsOk()
    {
        var response = await _client.GetAsync($"{Base}/validator?displayName=Ok&count=3");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UnexpectedException_Returns500WithGenericMessageOnly()
    {
        var response = await _client.GetAsync($"{Base}/unexpected");

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        var body = await response.Content.ReadAsStringAsync();
        body.ShouldNotContain(ErrorTestController.SecretDetail);
        body.ShouldNotContain("InvalidOperationException");
        var error = await response.ReadErrorAsync();
        error.Code.ShouldBe("internalError");
        error.Message.ShouldBe("An unexpected error occurred.");
    }

    [Fact]
    public async Task BadQueryNumber_Returns400WithField()
    {
        var response = await _client.GetAsync($"{Base}/number?page=abc");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var error = await response.ReadErrorAsync();
        error.Code.ShouldBe("validationFailed");
        error.Errors.ShouldNotBeNull().ShouldContainKey("page");
    }

    [Fact]
    public async Task MalformedJsonBody_Returns400WithField()
    {
        var response = await PostJsonAsync("""{ "name": "x", "quantity": "many" }""");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var error = await response.ReadErrorAsync();
        error.Code.ShouldBe("validationFailed");
        error.Errors.ShouldNotBeNull().ShouldContainKey("quantity");
    }

    [Fact]
    public async Task MissingBody_Returns400ForBody()
    {
        var response = await PostJsonAsync(string.Empty);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var error = await response.ReadErrorAsync();
        error.Code.ShouldBe("validationFailed");
        error.Errors.ShouldNotBeNull().ShouldNotBeEmpty();
    }

    [Fact]
    public async Task UnknownRoute_Returns404NotFound()
    {
        var response = await _client.GetAsync("/api/does-not-exist");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var error = await response.ReadErrorAsync();
        error.Code.ShouldBe("notFound");
        error.Message.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task WrongMethod_Returns405MethodNotAllowed()
    {
        var response = await _client.DeleteAsync($"{Base}/conflict");

        response.StatusCode.ShouldBe(HttpStatusCode.MethodNotAllowed);
        (await response.ReadErrorAsync()).Code.ShouldBe("methodNotAllowed");
    }

    [Fact]
    public async Task NonJsonBody_Returns415UnsupportedMediaType()
    {
        using var content = new StringContent("name=x", Encoding.UTF8, "text/plain");
        var response = await _client.PostAsync($"{Base}/body", content);

        response.StatusCode.ShouldBe(HttpStatusCode.UnsupportedMediaType);
        (await response.ReadErrorAsync()).Code.ShouldBe("unsupportedMediaType");
    }

    private async Task<HttpResponseMessage> PostJsonAsync(string json)
    {
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        return await _client.PostAsync($"{Base}/body", content);
    }
}

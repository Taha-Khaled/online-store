using FluentValidation;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Store.Application.Common.Errors;
using Store.Application.Common.Validation;
using ValidationException = Store.Application.Common.Errors.ValidationException;

namespace Store.API.Tests.Fixtures;

public sealed record TestInput(string? DisplayName, int Count);

public sealed record TestBody(string Name, int Quantity);

public sealed class TestInputValidator : AbstractValidator<TestInput>
{
    public TestInputValidator()
    {
        RuleFor(input => input.DisplayName).NotEmpty();
        RuleFor(input => input.Count).InclusiveBetween(1, 5);
    }
}

/// <summary>Test-only endpoints that raise each kind of failure (added by <see cref="ErrorTestApiFactory"/>).</summary>
[ApiController]
[Route("api/test-errors")]
public sealed class ErrorTestController : ControllerBase
{
    public const string SecretDetail = "secret-detail-7f3a";

    [HttpGet("validation")]
    public IActionResult Validation() =>
        throw new ValidationException(new Dictionary<string, string[]> { ["email"] = ["Email is required."] });

    [HttpGet("validator")]
    public async Task<IActionResult> Validator([FromQuery] TestInput input)
    {
        await new TestInputValidator().ValidateOrThrowAsync(input);
        return Ok();
    }

    [HttpGet("unauthorized")]
    public IActionResult ThrowUnauthorized() => throw new UnauthorizedException(ErrorCodes.InvalidCredentials, "Invalid email or password.");

    [HttpGet("not-found")]
    public IActionResult ThrowNotFound() => throw new NotFoundException(ErrorCodes.ProductNotFound, "Product not found.");

    [HttpGet("conflict")]
    public IActionResult ThrowConflict() => throw new ConflictException(ErrorCodes.EmailAlreadyExists, "Email already exists.");

    [HttpGet("bad-request")]
    public IActionResult ThrowBadRequest() => throw new BadRequestException(ErrorCodes.CartEmpty, "Your cart is empty.");

    [HttpGet("conflict-with-details")]
    public IActionResult ThrowConflictWithDetails() =>
        throw new ConflictException(
            ErrorCodes.CartChanged,
            "Some items in your cart changed.",
            new { items = new[] { new { itemId = 41, variantId = 12, status = "quantityReduced", maxQuantity = 1 } } });

    [HttpGet("customer-only")]
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = Store.API.Authorization.Policies.CustomerOnly)]
    public IActionResult CustomerOnly() => Ok();

    [HttpGet("unexpected")]
    public IActionResult Unexpected() => throw new InvalidOperationException(SecretDetail);

    [HttpGet("number")]
    public IActionResult Number([FromQuery] int page) => Ok(page);

    [HttpPost("body")]
    public IActionResult Body([FromBody] TestBody body) => Ok(body);
}

public sealed class ErrorTestApiFactory : StoreApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
            services.AddControllers().AddApplicationPart(typeof(ErrorTestController).Assembly));
    }
}

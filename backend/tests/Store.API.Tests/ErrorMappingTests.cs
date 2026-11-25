using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Shouldly;
using Store.API.Errors;

namespace Store.API.Tests;

/// <summary>Unit tests for the error mappings that HTTP tests cannot reach yet (research R6).</summary>
public class ErrorMappingTests
{
    [Theory]
    [InlineData(StatusCodes.Status401Unauthorized, "unauthorized")]
    [InlineData(StatusCodes.Status403Forbidden, "forbidden")]
    [InlineData(StatusCodes.Status404NotFound, "notFound")]
    [InlineData(StatusCodes.Status405MethodNotAllowed, "methodNotAllowed")]
    [InlineData(StatusCodes.Status415UnsupportedMediaType, "unsupportedMediaType")]
    [InlineData(StatusCodes.Status406NotAcceptable, "requestFailed")]
    public void ForStatus_ReturnsCodeAndMessage(int statusCode, string code)
    {
        var error = ErrorResponseWriter.ForStatus(statusCode);

        error.Code.ShouldBe(code);
        error.Message.ShouldNotBeNullOrWhiteSpace();
        error.Errors.ShouldBeNull();
    }

    [Fact]
    public void ModelState_RootKeyAndMissingMessage_MapToBodyAndFallbackMessage()
    {
        var modelState = new ModelStateDictionary();
        modelState.TryAddModelException("$", new InvalidOperationException());
        modelState.AddModelError("Address.City", "City is invalid.");

        var result = ModelStateErrors.CreateResponse(CreateContext(modelState)).ShouldBeOfType<BadRequestObjectResult>();

        var error = result.Value.ShouldBeOfType<ErrorResponse>();
        error.Code.ShouldBe("validationFailed");
        var errors = error.Errors.ShouldNotBeNull();
        errors["body"].ShouldBe(["The value is invalid."]);
        errors["address.city"].ShouldBe(["City is invalid."]);
    }

    private static ActionContext CreateContext(ModelStateDictionary modelState) =>
        new(new DefaultHttpContext(), new RouteData(), new ActionDescriptor(), modelState);
}

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Store.Application.Common.Errors;
using Store.Application.Common.Validation;

namespace Store.API.Errors;

/// <summary>Turns model binding failures (bad numbers, malformed JSON, missing body) into the error shape.</summary>
public static class ModelStateErrors
{
    private const string BodyField = "body";
    private const string FallbackMessage = "The value is invalid.";

    public static IActionResult CreateResponse(ActionContext context)
    {
        var errors = context.ModelState
            .Where(entry => entry.Value is { Errors.Count: > 0 })
            .GroupBy(entry => ToFieldName(entry.Key))
            .ToDictionary(
                group => group.Key,
                group => group.SelectMany(entry => entry.Value!.Errors).Select(ToMessage).Distinct().ToArray());

        return new BadRequestObjectResult(
            new ErrorResponse(ErrorCodes.ValidationFailed, ValidationException.DefaultMessage, errors));
    }

    private static string ToFieldName(string key)
    {
        var path = key.StartsWith("$.", StringComparison.Ordinal) ? key[2..] : key.TrimStart('$');
        return path.Length == 0 ? BodyField : FieldNames.ToCamelCasePath(path);
    }

    private static string ToMessage(ModelError error) =>
        string.IsNullOrWhiteSpace(error.ErrorMessage) ? FallbackMessage : error.ErrorMessage;
}

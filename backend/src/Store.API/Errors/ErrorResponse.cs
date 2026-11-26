using System.Text.Json.Serialization;

namespace Store.API.Errors;

/// <summary>
/// The single error shape (contracts/api.md): <c>errors</c> only for field validation, <c>details</c>
/// only for errors that carry structured data (003 research R10). Both are omitted when null.
/// </summary>
public sealed record ErrorResponse(
    string Code,
    string Message,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyDictionary<string, string[]>? Errors = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    object? Details = null);

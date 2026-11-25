using System.Text.Json.Serialization;

namespace Store.API.Errors;

/// <summary>The single error shape (contracts/api.md); <c>errors</c> only for validation failures.</summary>
public sealed record ErrorResponse(
    string Code,
    string Message,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyDictionary<string, string[]>? Errors = null);

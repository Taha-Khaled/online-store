namespace Store.Application.Common.Errors;

/// <summary>
/// Base for expected failures; the API maps each subtype to a status code. <see cref="Details"/>
/// carries structured data for the client (e.g. which cart items changed) and is omitted when null.
/// </summary>
public abstract class AppException(string code, string message, object? details = null) : Exception(message)
{
    public string Code { get; } = code;

    public object? Details { get; } = details;
}

public sealed class ValidationException(IReadOnlyDictionary<string, string[]> errors)
    : AppException(ErrorCodes.ValidationFailed, DefaultMessage)
{
    public const string DefaultMessage = "One or more fields are invalid.";

    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;
}

/// <summary>A 400 for a business rule that is not a field validation (e.g. <c>cartEmpty</c>).</summary>
public sealed class BadRequestException(string code, string message) : AppException(code, message);

public sealed class NotFoundException(string code, string message) : AppException(code, message);

public sealed class ConflictException(string code, string message, object? details = null)
    : AppException(code, message, details);

public sealed class UnauthorizedException(string code, string message) : AppException(code, message);

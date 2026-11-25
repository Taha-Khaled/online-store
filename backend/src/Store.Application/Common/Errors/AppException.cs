namespace Store.Application.Common.Errors;

/// <summary>Base for expected failures; the API maps each subtype to a status code.</summary>
public abstract class AppException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

public sealed class ValidationException(IReadOnlyDictionary<string, string[]> errors)
    : AppException(ErrorCodes.ValidationFailed, DefaultMessage)
{
    public const string DefaultMessage = "One or more fields are invalid.";

    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;
}

public sealed class NotFoundException(string code, string message) : AppException(code, message);

public sealed class ConflictException(string code, string message) : AppException(code, message);

public sealed class UnauthorizedException(string code, string message) : AppException(code, message);

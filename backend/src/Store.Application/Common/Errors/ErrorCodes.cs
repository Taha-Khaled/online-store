namespace Store.Application.Common.Errors;

/// <summary>Every error code the API returns (contracts/api.md "Error shape").</summary>
public static class ErrorCodes
{
    public const string ValidationFailed = "validationFailed";
    public const string Unauthorized = "unauthorized";
    public const string InvalidCredentials = "invalidCredentials";
    public const string InvalidRefreshToken = "invalidRefreshToken";
    public const string Forbidden = "forbidden";
    public const string NotFound = "notFound";
    public const string CategoryNotFound = "categoryNotFound";
    public const string ProductNotFound = "productNotFound";
    public const string MethodNotAllowed = "methodNotAllowed";
    public const string EmailAlreadyExists = "emailAlreadyExists";
    public const string UnsupportedMediaType = "unsupportedMediaType";
    public const string RequestFailed = "requestFailed";
    public const string InternalError = "internalError";
}

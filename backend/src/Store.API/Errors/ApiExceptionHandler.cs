using Microsoft.AspNetCore.Diagnostics;
using Store.Application.Common.Errors;

namespace Store.API.Errors;

/// <summary>Converts every exception to the error shape (FR-001–FR-003). Logs metadata only (FR-004).</summary>
public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    private const string GenericMessage = "An unexpected error occurred.";

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (statusCode, error) = Map(exception);

        if (statusCode == StatusCodes.Status500InternalServerError)
        {
            logger.LogError(
                exception,
                "Unhandled {ExceptionType} on {Method} {Path} (trace {TraceId})",
                exception.GetType().Name,
                httpContext.Request.Method,
                httpContext.Request.Path,
                httpContext.TraceIdentifier);
        }
        else
        {
            logger.LogInformation(
                "Request failed with {StatusCode} {ErrorCode} on {Method} {Path} (trace {TraceId})",
                statusCode,
                error.Code,
                httpContext.Request.Method,
                httpContext.Request.Path,
                httpContext.TraceIdentifier);
        }

        await ErrorResponseWriter.WriteAsync(httpContext, statusCode, error);
        return true;
    }

    private static (int StatusCode, ErrorResponse Error) Map(Exception exception) => exception switch
    {
        ValidationException validation => (StatusCodes.Status400BadRequest, new ErrorResponse(validation.Code, validation.Message, validation.Errors)),
        UnauthorizedException unauthorized => (StatusCodes.Status401Unauthorized, new ErrorResponse(unauthorized.Code, unauthorized.Message)),
        NotFoundException notFound => (StatusCodes.Status404NotFound, new ErrorResponse(notFound.Code, notFound.Message)),
        ConflictException conflict => (StatusCodes.Status409Conflict, new ErrorResponse(conflict.Code, conflict.Message)),
        _ => (StatusCodes.Status500InternalServerError, new ErrorResponse(ErrorCodes.InternalError, GenericMessage))
    };
}

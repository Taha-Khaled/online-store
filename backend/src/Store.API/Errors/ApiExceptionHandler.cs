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
        AppException app => (StatusFor(app), new ErrorResponse(app.Code, app.Message, Details: app.Details)),
        _ => (StatusCodes.Status500InternalServerError, new ErrorResponse(ErrorCodes.InternalError, GenericMessage))
    };

    private static int StatusFor(AppException exception) => exception switch
    {
        BadRequestException => StatusCodes.Status400BadRequest,
        UnauthorizedException => StatusCodes.Status401Unauthorized,
        NotFoundException => StatusCodes.Status404NotFound,
        ConflictException => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status500InternalServerError
    };
}

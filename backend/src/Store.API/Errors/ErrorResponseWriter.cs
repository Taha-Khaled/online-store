using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;
using Store.Application.Common.Errors;

namespace Store.API.Errors;

/// <summary>Writes the error shape for responses produced outside MVC (status code pages, auth events).</summary>
public static class ErrorResponseWriter
{
    public static Task WriteAsync(HttpContext context, int statusCode, ErrorResponse error)
    {
        context.Response.StatusCode = statusCode;
        return context.Response.WriteAsJsonAsync(error, JsonSerializerOptions.Web);
    }

    /// <summary>Status code pages handler: gives every body-less error status the error shape.</summary>
    public static Task WriteStatusCodePageAsync(StatusCodeContext context)
    {
        var statusCode = context.HttpContext.Response.StatusCode;
        return WriteAsync(context.HttpContext, statusCode, ForStatus(statusCode));
    }

    public static ErrorResponse ForStatus(int statusCode) => statusCode switch
    {
        StatusCodes.Status401Unauthorized => new(ErrorCodes.Unauthorized, "Authentication is required."),
        StatusCodes.Status403Forbidden => new(ErrorCodes.Forbidden, "You are not allowed to perform this action."),
        StatusCodes.Status404NotFound => new(ErrorCodes.NotFound, "The requested resource was not found."),
        StatusCodes.Status405MethodNotAllowed => new(ErrorCodes.MethodNotAllowed, "The HTTP method is not allowed for this resource."),
        StatusCodes.Status415UnsupportedMediaType => new(ErrorCodes.UnsupportedMediaType, "The request body must be JSON."),
        _ => new(ErrorCodes.RequestFailed, "The request could not be processed.")
    };
}

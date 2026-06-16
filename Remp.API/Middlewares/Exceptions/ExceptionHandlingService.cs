using System;
using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;

namespace Remp.API.Middlewares.Exceptions;

public class ExceptionHandlingService
{
    public async Task HandleExceptionAsync(HttpContext context)
    {
        var exceptionHandlerFeature = context.Features.Get<IExceptionHandlerFeature>();
        if (exceptionHandlerFeature == null) return;

        var exception = exceptionHandlerFeature.Error;
        // _logger.LogError(exception, "Unhandled exception occurred.");

        context.Response.ContentType = "application/json";
        int statusCode = GetStatusCode(exception);

        var errorResponse = new ErrorResponse(
            statusCode,
            GetErrorMessage(exception),
            exception.GetType().Name,
            context.Request.Path
        );

        context.Response.StatusCode = statusCode;
        await context.Response.WriteAsync(JsonSerializer.Serialize(errorResponse));
    }

    private static int GetStatusCode(Exception exception)
    {
        if (exception is ArgumentException)
        {
            return StatusCodes.Status400BadRequest;
        }
        else if (exception is KeyNotFoundException)
        {
            return StatusCodes.Status404NotFound;
        }
        else if (exception is UnauthorizedAccessException)
        {
            return StatusCodes.Status401Unauthorized;
        }
        else
        {
            return StatusCodes.Status500InternalServerError;
        }
    }

    private static string GetErrorMessage(Exception exception)
    {
        if (exception is ArgumentException)
        {
            return "Invalid input provided.";
        }
        else if (exception is KeyNotFoundException)
        {
            return "Requested resource was not found.";
        }
        else if (exception is UnauthorizedAccessException)
        {
            return "You are not authorized to access this resource.";
        }
        else
        {
            return "An unexpected error occurred. Please try again later.";
        }
    }
}

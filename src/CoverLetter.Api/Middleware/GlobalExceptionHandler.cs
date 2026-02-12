using System.Net;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Refit;
using ProblemDetails = Microsoft.AspNetCore.Mvc.ProblemDetails;

namespace CoverLetter.Api.Middleware;

/// <summary>
/// Global exception handler that catches all unhandled exceptions
/// and returns a structured ProblemDetails response.
/// </summary>
public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // Handle validation errors separately to avoid redundant processing
        if (exception is ValidationException validationException)
        {
            await HandleValidationExceptionAsync(
                httpContext,
                validationException,
                cancellationToken);
            return true;
        }

        var (statusCode, title, detail) = MapException(exception);

        // Log client errors at Debug level, unexpected errors at Error level
        if (exception is BadHttpRequestException)
        {
            logger.LogDebug(
                "Bad HTTP request for {Path}: {Message}",
                httpContext.Request.Path,
                exception.Message);
        }
        else
        {
            logger.LogError(
                exception,
                "Unhandled exception occurred: {ExceptionType} - {Message}",
                exception.GetType().Name,
                exception.Message);
        }

        var problemDetails = new ProblemDetails
        {
            Status = (int)statusCode,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path,
            Type = GetProblemType(statusCode)
        };

        problemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;

        httpContext.Response.StatusCode = (int)statusCode;
        httpContext.Response.ContentType = "application/problem+json";

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }

    private async ValueTask HandleValidationExceptionAsync(
        HttpContext httpContext,
        ValidationException validationException,
        CancellationToken cancellationToken)
    {
        // Build errors dictionary once
        var errors = validationException.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(
                g => g.Key,
                g => g.Select(e => e.ErrorMessage).ToArray());

        // Log at Debug level - validation is expected client behavior
        logger.LogDebug(
            "Validation failed for {Path}: {ErrorCount} error(s)",
            httpContext.Request.Path,
            errors.Count);

        var problemDetails = new ValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Validation Failed",
            Detail = "One or more validation errors occurred. See 'errors' for details.",
            Instance = httpContext.Request.Path,
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1"
        };

        problemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;

        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        httpContext.Response.ContentType = "application/problem+json";

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
    }

    private static (HttpStatusCode StatusCode, string Title, string Detail) MapException(Exception exception)
    {
        return exception switch
        {
            BadHttpRequestException badHttpEx => (
                HttpStatusCode.BadRequest,
                "Invalid Request",
                $"The request is malformed or invalid: {badHttpEx.Message}"),

            ApiException apiEx => (
                HttpStatusCode.BadGateway,
                "External API Error",
                $"Error communicating with external service: {apiEx.Message}"),

            HttpRequestException httpEx => (
                HttpStatusCode.ServiceUnavailable,
                "Service Unavailable",
                $"Failed to connect to external service: {httpEx.Message}"),

            TaskCanceledException or OperationCanceledException => (
                HttpStatusCode.RequestTimeout,
                "Request Timeout",
                "The request was cancelled or timed out"),

            ArgumentException argEx => (
                HttpStatusCode.BadRequest,
                "Invalid Argument",
                argEx.Message),

            UnauthorizedAccessException => (
                HttpStatusCode.Unauthorized,
                "Unauthorized",
                "You are not authorized to perform this action"),

            NotImplementedException => (
                HttpStatusCode.NotImplemented,
                "Not Implemented",
                "This feature is not yet implemented"),

            _ => (
                HttpStatusCode.InternalServerError,
                "Internal Server Error",
                "An unexpected error occurred. Please try again later.")
        };
    }

    private static string GetProblemType(HttpStatusCode statusCode)
    {
        return statusCode switch
        {
            HttpStatusCode.BadRequest => "https://tools.ietf.org/html/rfc7231#section-6.5.1",
            HttpStatusCode.Unauthorized => "https://tools.ietf.org/html/rfc7235#section-3.1",
            HttpStatusCode.NotFound => "https://tools.ietf.org/html/rfc7231#section-6.5.4",
            HttpStatusCode.RequestTimeout => "https://tools.ietf.org/html/rfc7231#section-6.5.7",
            HttpStatusCode.InternalServerError => "https://tools.ietf.org/html/rfc7231#section-6.6.1",
            HttpStatusCode.BadGateway => "https://tools.ietf.org/html/rfc7231#section-6.6.3",
            HttpStatusCode.ServiceUnavailable => "https://tools.ietf.org/html/rfc7231#section-6.6.4",
            _ => "https://tools.ietf.org/html/rfc7231#section-6.6.1"
        };
    }
}

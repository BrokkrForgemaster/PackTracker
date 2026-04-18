using System.Net;
using System.Text.Json;
using FluentValidation;
using PackTracker.Application.Interfaces;
using PackTracker.Common.DTOs;

namespace PackTracker.Api.Middleware;

/// <summary>
/// Middleware that catches unhandled exceptions and returns a standardized error response.
/// </summary>
public class ExceptionHandlingMiddleware
{
    #region Fields

    private readonly RequestDelegate _next;
    private readonly ILoggingService<ExceptionHandlingMiddleware> _logger;

    #endregion

    #region Constructor

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILoggingService<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    #endregion

    #region Public Methods

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            var traceId = context.TraceIdentifier;
            var statusCode = ex is ValidationException
                ? (int)HttpStatusCode.BadRequest
                : (int)HttpStatusCode.InternalServerError;
            var message = ex is ValidationException validationException
                ? "Validation failed"
                : ex.Message;
            var errors = ex is ValidationException fluentValidationException
                ? fluentValidationException.Errors
                    .GroupBy(x => x.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(x => x.ErrorMessage).ToArray())
                : new Dictionary<string, string[]>
                {
                    ["StackTrace"] = new[] { ex.StackTrace ?? "No stack trace available" }
                };

            _logger.LogError(
                ex,
                "🔥 Unhandled exception at {Path} TraceId={TraceId}",
                context.Request.Path,
                traceId);

            if (context.Response.HasStarted)
                return;

            var error = new ErrorResponse
            {
                Message = message,
                TraceId = traceId,
                StatusCode = statusCode,
                Errors = errors
            };

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = error.StatusCode;

            var json = JsonSerializer.Serialize(error,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

            await context.Response.WriteAsync(json);
        }
    }

    #endregion
}

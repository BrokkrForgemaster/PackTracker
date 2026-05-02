using System.Net;
using System.Text.Json;
using FluentValidation;
using PackTracker.Common.DTOs;
using PackTracker.Application.Interfaces;
using PackTracker.Application.Admin.Common;

namespace PackTracker.Api.Middleware;

/// <summary name="ExecptionHandlingMiddleware">
/// Middleware that catches unhandled exceptions and returns a standardized error response.
/// </summary>
public class ExceptionHandlingMiddleware
{
    #region Fields

    private readonly RequestDelegate _next;
    private readonly ILoggingService<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    #endregion

    #region Constructor

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILoggingService<ExceptionHandlingMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
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
                : ex is AdminAuthorizationException
                    ? (int)HttpStatusCode.Forbidden
                : (int)HttpStatusCode.InternalServerError;
            var message = ex is ValidationException validationException
                ? "Validation failed"
                : ex is AdminAuthorizationException adminAuthorizationException
                    ? adminAuthorizationException.Message
                : "An unexpected error occurred.";
            var errors = ex is ValidationException fluentValidationException
                ? fluentValidationException.Errors
                    .GroupBy(x => x.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(x => x.ErrorMessage).ToArray())
                : BuildServerErrorDetails(traceId, ex);

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

    private Dictionary<string, string[]>? BuildServerErrorDetails(string traceId, Exception ex)
    {
        if (_environment.IsDevelopment())
        {
            return new Dictionary<string, string[]>
            {
                ["Exception"] = [ex.Message],
                ["TraceId"] = [traceId]
            };
        }

        return new Dictionary<string, string[]>
        {
            ["TraceId"] = [traceId]
        };
    }

    #endregion
}

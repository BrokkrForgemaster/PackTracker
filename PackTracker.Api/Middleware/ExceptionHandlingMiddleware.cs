using System.Net;
using System.Text.Json;
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

            _logger.LogError(
                ex,
                "🔥 Unhandled exception at {Path} TraceId={TraceId}",
                context.Request.Path,
                traceId);

            if (context.Response.HasStarted)
                return;

            var error = new ErrorResponse
            {
                Message = "An unexpected error occurred",
                TraceId = traceId,
                StatusCode = (int)HttpStatusCode.InternalServerError
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
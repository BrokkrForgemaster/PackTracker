using Microsoft.AspNetCore.Mvc;
using PackTracker.Common.DTOs;
using System.Text.Json;

namespace PackTracker.Api.Middleware;

/// <summary>
/// Middleware that standardizes validation error responses.
/// </summary>
public class ValidationHandlingMiddleware
{
    #region Fields

    private readonly RequestDelegate _next;
    private readonly ILogger<ValidationHandlingMiddleware> _logger;

    #endregion

    #region Constructor

    public ValidationHandlingMiddleware(
        RequestDelegate next,
        ILogger<ValidationHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    #endregion

    #region Public Methods

    public async Task InvokeAsync(HttpContext context)
    {
        await _next(context);

        if (context.Response.HasStarted) return;

        if (context.Response.StatusCode == StatusCodes.Status400BadRequest &&
            context.Items.TryGetValue("ValidationProblemDetails", out var problemObj) &&
            problemObj is ValidationProblemDetails problem)
        {
            var traceId = context.TraceIdentifier;

            _logger.LogWarning(
                "⚠️ Validation failed Path={Path} TraceId={TraceId}",
                context.Request.Path,
                traceId);

            var errorResponse = new ErrorResponse
            {
                Message = "Validation failed",
                TraceId = traceId,
                StatusCode = StatusCodes.Status400BadRequest,
                Errors = problem.Errors.ToDictionary(k => k.Key, v => v.Value)
            };

            context.Response.ContentType = "application/json";

            var json = JsonSerializer.Serialize(errorResponse,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

            await context.Response.WriteAsync(json);
        }
    }

    #endregion
}
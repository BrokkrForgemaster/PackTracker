using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PackTracker.Common.DTOs;
using System.Text.Json;

namespace PackTracker.Api.Middleware;

public class ValidationHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ValidationHandlingMiddleware> _logger;

    public ValidationHandlingMiddleware(RequestDelegate next, ILogger<ValidationHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        await _next(context);

        if (context.Response.StatusCode == StatusCodes.Status400BadRequest &&
            context.Items.TryGetValue("ValidationProblemDetails", out var problemObj) &&
            problemObj is ValidationProblemDetails problem)
        {
            _logger.LogWarning(
                "Validation failed. Path={Path} TraceId={TraceId} Errors={Errors}",
                context.Request.Path,
                context.TraceIdentifier,
                string.Join("; ", problem.Errors.Select(e => $"{e.Key}: {string.Join(", ", e.Value)}")));

            var errorResponse = new ErrorResponse
            {
                Message = "Validation failed",
                TraceId = context.TraceIdentifier,
                StatusCode = StatusCodes.Status400BadRequest,
                Errors = problem.Errors.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value
                )
            };

            context.Response.ContentType = "application/json";
            var json = JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            await context.Response.WriteAsync(json);
        }
    }
}

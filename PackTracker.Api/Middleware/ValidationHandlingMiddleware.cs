using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using PackTracker.Common.DTOs;
using System.Text.Json;

namespace PackTracker.Api.Middleware;

public class ValidationHandlingMiddleware
{
    private readonly RequestDelegate _next;

    public ValidationHandlingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        await _next(context);

        // Check if model validation failed
        if (context.Response.StatusCode == StatusCodes.Status400BadRequest &&
            context.Items.TryGetValue("ValidationProblemDetails", out var problemObj) &&
            problemObj is ValidationProblemDetails problem)
        {
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
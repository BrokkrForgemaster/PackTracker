using System.Diagnostics;
using PackTracker.Application.Interfaces;

namespace PackTracker.Api.Middleware;

/// <summary>
/// Middleware for logging incoming HTTP requests and responses.
/// </summary>
public class RequestLoggingMiddleware
{
    #region Fields

    private readonly RequestDelegate _next;
    private readonly ILoggingService<RequestLoggingMiddleware> _logger;

    #endregion

    #region Constructor

    public RequestLoggingMiddleware(
        RequestDelegate next,
        ILoggingService<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    #endregion

    #region Public Methods

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.TraceIdentifier;
        var sw = Stopwatch.StartNew();

        _logger.LogInformation(
            "➡️ HTTP START {Method} {Path} TraceId={TraceId}",
            context.Request.Method,
            context.Request.Path,
            correlationId);

        await _next(context);

        sw.Stop();

        _logger.LogInformation(
            "⬅️ HTTP END {Method} {Path} {StatusCode} in {Elapsed}ms TraceId={TraceId}",
            context.Request.Method,
            context.Request.Path,
            context.Response.StatusCode,
            sw.ElapsedMilliseconds,
            correlationId);
    }

    #endregion
}
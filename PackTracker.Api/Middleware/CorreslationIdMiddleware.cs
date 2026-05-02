using System.Diagnostics;

namespace PackTracker.Api.Middleware;

/// <summary name="CorreltationIdMiddleware">
/// Middleware that ensures every request has a correlation ID for tracing across logs and services.
/// </summary>
public class CorrelationIdMiddleware
{
    #region Fields

    private const string HeaderName = "X-Correlation-ID";
    private readonly RequestDelegate _next;

    #endregion

    #region Constructor

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    #endregion

    #region Public Methods

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var correlationId = context.Request.Headers.TryGetValue(HeaderName, out var existing)
            ? existing.ToString()
            : Activity.Current?.Id ?? Guid.NewGuid().ToString();

        // ✅ Set for entire request lifecycle
        context.TraceIdentifier = correlationId;
        context.Items[HeaderName] = correlationId;

        // ✅ Add to response
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        await _next(context);
    }

    #endregion
}
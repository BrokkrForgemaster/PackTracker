using System.Collections;
using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;
using PackTracker.Application.Interfaces;

namespace PackTracker.Application.Common.Behaviors;

/// <summary>
/// Provides targeted diagnostic logging for database-bound queries in the Requests and Procurement systems.
/// This behavior is designed to be ultra-lean, avoiding extra database calls and only logging
/// for specific namespaces.
/// </summary>
public class DataAccessLoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<DataAccessLoggingBehavior<TRequest, TResponse>> _logger;
    private readonly ICurrentUserService _currentUser;

    public DataAccessLoggingBehavior(
        ILogger<DataAccessLoggingBehavior<TRequest, TResponse>> logger,
        ICurrentUserService currentUser)
    {
        _logger = logger;
        _currentUser = currentUser;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var requestType = typeof(TRequest);
        var requestNamespace = requestType.Namespace;
        var requestName = requestType.Name;

        // Constraint: Explicit exit for non-target namespaces using StartsWith
        if (requestNamespace == null || 
            (!requestNamespace.StartsWith("PackTracker.Application.Requests", StringComparison.Ordinal) && 
             !requestNamespace.StartsWith("PackTracker.Application.Crafting", StringComparison.Ordinal)))
        {
            return await next();
        }

        // Constraint: Explicit exit for Commands (Query-only detection)
        if (!requestName.EndsWith("Query", StringComparison.Ordinal))
        {
            return await next();
        }

        var sw = Stopwatch.StartNew();
        var response = await next();
        sw.Stop();

        // Constraint: Result Count extraction (handling collections without triggering double enumeration)
        int? resultCount = GetResultCount(response);

        var discordId = _currentUser.UserId;
        
        // Constraint: Do NOT attempt ProfileId lookup. Only log if available in request.
        // We try to extract it from the request object if it follows common naming patterns.
        var profileId = GetProfileIdFromRequest(request) ?? "NULL";

        if (resultCount == 0)
        {
            // Constraint: Log WARNING for zero results in target modules with full context
            // We extract a summary of the request properties as "filters" for context.
            var filters = GetFilterSummary(request);

            _logger.LogWarning(
                "[DIAGNOSTIC] Query {QueryName} for User {DiscordId}/{ProfileId} returned {ResultCount} items. Filters=[{Filters}] Elapsed={Elapsed}ms",
                requestName, discordId, profileId, 0, filters, sw.ElapsedMilliseconds);
        }
        else
        {
            // Constraint: Log DEBUG for successful queries to avoid log spam
            _logger.LogDebug(
                "[DIAGNOSTIC] Query {QueryName} for User {DiscordId}/{ProfileId} returned {ResultCount} items. Elapsed={Elapsed}ms",
                requestName, discordId, profileId, resultCount, sw.ElapsedMilliseconds);
        }

        return response;
    }

    private static string GetFilterSummary(TRequest request)
    {
        // Simple extraction of value-type/string properties that likely represent filters.
        // We avoid properties like "Notes", "Description", or "Content" to stay safe.
        var props = typeof(TRequest).GetProperties()
            .Where(p => p.PropertyType.IsValueType || p.PropertyType == typeof(string))
            .Where(p => !p.Name.Contains("Notes") && !p.Name.Contains("Description") && !p.Name.Contains("Content"))
            .Select(p => $"{p.Name}={p.GetValue(request) ?? "NULL"}");

        return string.Join(", ", props);
    }

    private static string? GetProfileIdFromRequest(TRequest request)
    {
        var type = typeof(TRequest);
        var prop = type.GetProperty("ProfileId") ?? 
                   type.GetProperty("RequesterProfileId") ?? 
                   type.GetProperty("CurrentProfileId");

        return prop?.GetValue(request)?.ToString();
    }

    private static int? GetResultCount(TResponse response)
    {
        if (response == null || response is string) return null;

        if (response is ICollection collection)
        {
            return collection.Count;
        }

        // Handle generic IReadOnlyCollection or lists that don't implement ICollection
        var type = response.GetType();
        var countProperty = type.GetProperty("Count") ?? type.GetProperty("Length");
        
        if (countProperty != null && countProperty.PropertyType == typeof(int))
        {
            return (int?)countProperty.GetValue(response);
        }

        return null;
    }
}

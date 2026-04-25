using System;

namespace PackTracker.Presentation.Services;

public static class DirectMessageChannel
{
    public static string BuildCanonical(string? currentUsername, string counterpartUsername)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(counterpartUsername);

        var other = counterpartUsername.Trim().ToLowerInvariant();
        var self = currentUsername?.Trim().ToLowerInvariant() ?? string.Empty;
        var parts = new[] { self, other };
        Array.Sort(parts, StringComparer.Ordinal);
        return $"dm:{parts[0]}:{parts[1]}";
    }

    public static string BuildFallback(string? currentUsername, string counterpartUsername)
    {
        if (string.IsNullOrWhiteSpace(currentUsername))
            return $"direct:{counterpartUsername.Trim().ToLowerInvariant()}";

        return BuildCanonical(currentUsername, counterpartUsername);
    }

    public static bool TryGetCounterpart(string? channel, string? currentUsername, out string counterpartUsername)
    {
        counterpartUsername = string.Empty;
        if (string.IsNullOrWhiteSpace(channel))
            return false;

        if (channel.StartsWith("direct:", StringComparison.OrdinalIgnoreCase))
        {
            var legacy = channel["direct:".Length..].Trim();
            if (string.IsNullOrWhiteSpace(legacy))
                return false;

            counterpartUsername = legacy;
            return true;
        }

        if (!channel.StartsWith("dm:", StringComparison.OrdinalIgnoreCase))
            return false;

        var parts = channel.Split(':', 3, StringSplitOptions.None);
        if (parts.Length != 3)
            return false;

        var self = currentUsername?.Trim().ToLowerInvariant() ?? string.Empty;
        var first = parts[1];
        var second = parts[2];

        counterpartUsername = string.Equals(first, self, StringComparison.OrdinalIgnoreCase)
            ? second
            : first;

        return !string.IsNullOrWhiteSpace(counterpartUsername);
    }
}

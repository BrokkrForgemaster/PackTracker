using System.Collections.Concurrent;

namespace PackTracker.Infrastructure.Services
{
    public static class TokenCacheService
    {
        private static readonly ConcurrentDictionary<string, TokenPayload> _cache = new();

        public static void Store(string state, TokenPayload payload)
        {
            _cache[state] = payload;
        }

        public static TokenPayload? Retrieve(string state)
        {
            if (_cache.TryRemove(state, out var payload))
                return payload;
            return null;
        }
    }

    public record TokenPayload(string access_token, string refresh_token, int expires_in);
}
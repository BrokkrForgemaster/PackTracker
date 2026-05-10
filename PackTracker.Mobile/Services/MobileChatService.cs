using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;

namespace PackTracker.Mobile.Services;

public record ChatMessage(
    string Id,
    string Channel,
    string Sender,
    string SenderDisplayName,
    string Content,
    DateTime SentAt,
    string? SenderRole);

public record OnlineMember(
    string Username,
    string DisplayName,
    string? Role);

public sealed class MobileChatService : IAsyncDisposable
{
    private readonly MobileSessionService _session;
    private HubConnection? _connection;
    private readonly HashSet<string> _joinedChannels = new(StringComparer.OrdinalIgnoreCase);

    public string? CurrentUsername { get; set; }

    public bool IsConnected =>
        _connection?.State == HubConnectionState.Connected;

    public event Action<ChatMessage>? MessageReceived;
    public event Action<string, string, string>? MessageEdited;   // channel, msgId, newContent
    public event Action<string, string>? MessageDeleted;          // channel, msgId
    public event Action<IReadOnlyList<OnlineMember>>? PresenceUpdated;
    public event Action<bool>? ConnectionStateChanged;

    public MobileChatService(MobileSessionService session)
    {
        _session = session;
    }

    // ──────────────────────────────────────────────────────────────────
    // Connection management
    // ──────────────────────────────────────────────────────────────────

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        if (_connection is not null)
            return;

        var baseUrl = _session.GetApiBaseUrl();
        var hubUrl  = $"{baseUrl}/hubs/requests";

        _connection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.AccessTokenProvider = () => _session.GetAccessTokenAsync()!;
            })
            .WithAutomaticReconnect()
            .Build();

        // Register all server-to-client handlers
        _connection.On<JsonElement>("ReceiveLobbyMessage",   OnReceiveLobbyMessage);
        _connection.On<JsonElement>("ReceiveDirectMessage",  OnReceiveDirectMessage);
        _connection.On<JsonElement>("LobbyHistory",          OnLobbyHistory);
        _connection.On<JsonElement>("LobbyMessageEdited",    OnLobbyMessageEdited);
        _connection.On<JsonElement>("LobbyMessageDeleted",   OnLobbyMessageDeleted);
        _connection.On<JsonElement>("PresenceUpdated",       OnPresenceUpdated);
        _connection.On<JsonElement>("PendingDirectMessages", OnPendingDirectMessages);

        _connection.Reconnected += async _ =>
        {
            ConnectionStateChanged?.Invoke(true);
            // Rejoin all previously joined channels
            var channels = _joinedChannels.ToList();
            foreach (var channel in channels)
            {
                try
                {
                    await _connection.InvokeAsync("JoinLobby", channel).ConfigureAwait(false);
                    await _connection.InvokeAsync("GetLobbyHistory", channel).ConfigureAwait(false);
                }
                catch
                {
                    // Best-effort
                }
            }
        };

        _connection.Reconnecting += _ =>
        {
            ConnectionStateChanged?.Invoke(false);
            return Task.CompletedTask;
        };

        _connection.Closed += _ =>
        {
            ConnectionStateChanged?.Invoke(false);
            return Task.CompletedTask;
        };

        await _connection.StartAsync(ct).ConfigureAwait(false);
        ConnectionStateChanged?.Invoke(true);
    }

    public async Task JoinChannelAsync(string channel)
    {
        EnsureConnected();
        _joinedChannels.Add(channel);
        await _connection!.InvokeAsync("JoinLobby", channel).ConfigureAwait(false);
        await _connection!.InvokeAsync("GetLobbyHistory", channel).ConfigureAwait(false);
    }

    public async Task SendMessageAsync(string channel, string content)
    {
        EnsureConnected();
        await _connection!.InvokeAsync("SendLobbyMessage", channel, content).ConfigureAwait(false);
    }

    public async Task SendDirectMessageAsync(string username, string content)
    {
        EnsureConnected();
        await _connection!.InvokeAsync("SendDirectMessage", username, content).ConfigureAwait(false);
    }

    public async Task GetDirectMessageHistoryAsync(string username)
    {
        EnsureConnected();
        await _connection!.InvokeAsync("GetDirectMessageHistory", username).ConfigureAwait(false);
    }

    public async Task DisconnectAsync()
    {
        if (_connection is null)
            return;

        await _connection.StopAsync().ConfigureAwait(false);
        await _connection.DisposeAsync().ConfigureAwait(false);
        _connection = null;
        _joinedChannels.Clear();
        ConnectionStateChanged?.Invoke(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync().ConfigureAwait(false);
            _connection = null;
        }
    }

    // ──────────────────────────────────────────────────────────────────
    // SignalR event handlers
    // ──────────────────────────────────────────────────────────────────

    private void OnReceiveLobbyMessage(JsonElement json)
    {
        var msg = ParseChatMessage(json);
        if (msg is not null)
            MessageReceived?.Invoke(msg);
    }

    private void OnReceiveDirectMessage(JsonElement json)
    {
        var counterpart = GetString(json, "counterpartUsername", "CounterpartUsername");
        if (counterpart is null)
            return;

        var channel = BuildDmChannelKey(CurrentUsername ?? string.Empty, counterpart);

        // Build the ChatMessage with the computed DM channel key
        var id          = GetString(json, "id",                 "Id")                ?? Guid.NewGuid().ToString();
        var sender      = GetString(json, "sender",             "Sender")            ?? string.Empty;
        var displayName = GetString(json, "senderDisplayName",  "SenderDisplayName") ?? sender;
        var content     = GetString(json, "content",            "Content")           ?? string.Empty;
        var role        = GetString(json, "senderRole",         "SenderRole");
        var sentAt      = GetDateTime(json, "sentAt",           "SentAt");

        var msg = new ChatMessage(id, channel, sender, displayName, content, sentAt, role);
        MessageReceived?.Invoke(msg);
    }

    private void OnLobbyHistory(JsonElement json)
    {
        var channel  = GetString(json, "channel",  "Channel") ?? string.Empty;
        if (!json.TryGetProperty("messages", out var msgs) &&
            !json.TryGetProperty("Messages", out msgs))
            return;

        if (msgs.ValueKind != JsonValueKind.Array)
            return;

        foreach (var item in msgs.EnumerateArray())
        {
            var msg = ParseChatMessage(item, channel);
            if (msg is not null)
                MessageReceived?.Invoke(msg);
        }
    }

    private void OnLobbyMessageEdited(JsonElement json)
    {
        var channel    = GetString(json, "channel",    "Channel")    ?? string.Empty;
        var msgId      = GetString(json, "messageId",  "MessageId")  ?? string.Empty;
        var newContent = GetString(json, "newContent", "NewContent") ?? string.Empty;
        MessageEdited?.Invoke(channel, msgId, newContent);
    }

    private void OnLobbyMessageDeleted(JsonElement json)
    {
        var channel = GetString(json, "channel",   "Channel")   ?? string.Empty;
        var msgId   = GetString(json, "messageId", "MessageId") ?? string.Empty;
        MessageDeleted?.Invoke(channel, msgId);
    }

    private void OnPresenceUpdated(JsonElement json)
    {
        if (json.ValueKind != JsonValueKind.Array)
            return;

        var members = new List<OnlineMember>();
        foreach (var item in json.EnumerateArray())
        {
            var username    = GetString(item, "username",    "Username")    ?? string.Empty;
            var displayName = GetString(item, "displayName", "DisplayName") ?? username;
            var role        = GetString(item, "role",        "Role");
            members.Add(new OnlineMember(username, displayName, role));
        }

        PresenceUpdated?.Invoke(members.AsReadOnly());
    }

    private void OnPendingDirectMessages(JsonElement json)
    {
        if (json.ValueKind != JsonValueKind.Array)
            return;

        foreach (var item in json.EnumerateArray())
        {
            var counterpart = GetString(item, "counterpartUsername", "CounterpartUsername");
            if (counterpart is null)
                continue;

            var channel = BuildDmChannelKey(CurrentUsername ?? string.Empty, counterpart);
            var id          = GetString(item, "id",                "Id")                ?? Guid.NewGuid().ToString();
            var sender      = GetString(item, "sender",            "Sender")            ?? string.Empty;
            var displayName = GetString(item, "senderDisplayName", "SenderDisplayName") ?? sender;
            var content     = GetString(item, "content",           "Content")           ?? string.Empty;
            var role        = GetString(item, "senderRole",        "SenderRole");
            var sentAt      = GetDateTime(item, "sentAt",          "SentAt");

            var msg = new ChatMessage(id, channel, sender, displayName, content, sentAt, role);
            MessageReceived?.Invoke(msg);
        }
    }

    // ──────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────

    private void EnsureConnected()
    {
        if (_connection is null)
            throw new InvalidOperationException("Not connected. Call ConnectAsync first.");
    }

    private static ChatMessage? ParseChatMessage(JsonElement json, string? channelOverride = null)
    {
        var id          = GetString(json, "id",                "Id")                ?? Guid.NewGuid().ToString();
        var channel     = channelOverride ?? GetString(json, "channel", "Channel") ?? string.Empty;
        var sender      = GetString(json, "sender",            "Sender")            ?? string.Empty;
        var displayName = GetString(json, "senderDisplayName", "SenderDisplayName") ?? sender;
        var content     = GetString(json, "content",           "Content")           ?? string.Empty;
        var role        = GetString(json, "senderRole",        "SenderRole");
        var sentAt      = GetDateTime(json, "sentAt",          "SentAt");

        return new ChatMessage(id, channel, sender, displayName, content, sentAt, role);
    }

    private static string? GetString(JsonElement json, string camelKey, string pascalKey)
    {
        if (json.TryGetProperty(camelKey, out var prop) && prop.ValueKind == JsonValueKind.String)
            return prop.GetString();
        if (json.TryGetProperty(pascalKey, out prop) && prop.ValueKind == JsonValueKind.String)
            return prop.GetString();
        return null;
    }

    private static DateTime GetDateTime(JsonElement json, string camelKey, string pascalKey)
    {
        if (json.TryGetProperty(camelKey, out var prop) && prop.ValueKind == JsonValueKind.String)
        {
            if (DateTime.TryParse(prop.GetString(), out var dt))
                return dt.ToLocalTime();
        }
        if (json.TryGetProperty(pascalKey, out prop) && prop.ValueKind == JsonValueKind.String)
        {
            if (DateTime.TryParse(prop.GetString(), out var dt))
                return dt.ToLocalTime();
        }
        return DateTime.Now;
    }

    /// <summary>
    /// Builds the canonical DM channel key: dm:{lower(a)}:{lower(b)} where a &lt; b alphabetically.
    /// </summary>
    public static string BuildDmChannelKey(string userA, string userB)
    {
        var a = userA.ToLowerInvariant();
        var b = userB.ToLowerInvariant();
        return string.Compare(a, b, StringComparison.Ordinal) <= 0
            ? $"dm:{a}:{b}"
            : $"dm:{b}:{a}";
    }

    /// <summary>
    /// Extracts the counterpart username from a DM channel key given the current user.
    /// Returns null if the channel key is not a valid DM key.
    /// </summary>
    public static string? TryGetDmCounterpart(string channelKey, string currentUsername)
    {
        if (!channelKey.StartsWith("dm:", StringComparison.OrdinalIgnoreCase))
            return null;

        var parts = channelKey.Split(':');
        if (parts.Length != 3)
            return null;

        var lower = currentUsername.ToLowerInvariant();
        if (parts[1].Equals(lower, StringComparison.OrdinalIgnoreCase))
            return parts[2];
        if (parts[2].Equals(lower, StringComparison.OrdinalIgnoreCase))
            return parts[1];

        return null;
    }
}

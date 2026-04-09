using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using PackTracker.Application.Interfaces;

namespace PackTracker.Presentation.Services;

/// <summary>
/// Manages the SignalR connection for real-time chat and presence features.
/// Registered as a singleton so the connection persists for the application lifetime.
/// </summary>
public class SignalRChatService : IAsyncDisposable
{
    private HubConnection? _connection;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<SignalRChatService> _logger;

    // Events the ViewModel subscribes to
    public event Action<ChatMessageDto>? MessageReceived;
    public event Action<ChatMessageDto>? RequestMessageReceived;
    public event Action<IReadOnlyList<OnlineUserDto>>? PresenceUpdated;
    public event Action<bool>? ConnectionStateChanged;
    public event Action<Guid>? CraftingRequestCreated;
    public event Action<Guid>? CraftingRequestUpdated;
    public event Action<Guid>? AssistanceRequestCreated;
    public event Action<Guid>? AssistanceRequestUpdated;

    public bool IsConnected => _connection?.State == HubConnectionState.Connected;

    public SignalRChatService(ISettingsService settingsService, ILogger<SignalRChatService> logger)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Builds the hub connection and starts it.
    /// Safe to call multiple times — skips if already connected.
    /// </summary>
    public async Task ConnectAsync()
    {
        if (_connection?.State == HubConnectionState.Connected)
            return;

        var settings = _settingsService.GetSettings();
        var baseUrl = (settings.ApiBaseUrl ?? "http://localhost:5001").TrimEnd('/');
        var hubUrl = $"{baseUrl}/hubs/requests";
        var token = settings.JwtToken;

        _connection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.AccessTokenProvider = () => Task.FromResult<string?>(token);
            })
            .WithAutomaticReconnect()
            .Build();

        // ── Message handlers ──────────────────────────────────────────
        _connection.On<JsonElement>("ReceiveLobbyMessage", json =>
        {
            try
            {
                var msg = ParseChatMessage(json);
                if (msg != null)
                    MessageReceived?.Invoke(msg);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse ReceiveLobbyMessage payload.");
            }
        });

        _connection.On<JsonElement>("LobbyHistory", json =>
        {
            try
            {
                string channel = string.Empty;
                if (json.TryGetProperty("channel", out var ch) || json.TryGetProperty("Channel", out ch))
                    channel = ch.GetString() ?? string.Empty;

                JsonElement messagesEl = default;
                var hasMessages = json.TryGetProperty("messages", out messagesEl)
                               || json.TryGetProperty("Messages", out messagesEl);

                if (!hasMessages || messagesEl.ValueKind != JsonValueKind.Array)
                    return;

                foreach (var item in messagesEl.EnumerateArray())
                {
                    var msg = ParseChatMessage(item, channel);
                    if (msg != null)
                        MessageReceived?.Invoke(msg);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse LobbyHistory payload.");
            }
        });

        _connection.On<JsonElement>("ReceiveRequestMessage", json =>
        {
            try
            {
                var id = GetString(json, "id", "Id") ?? Guid.NewGuid().ToString();
                var requestId = GetString(json, "requestId", "RequestId") ?? string.Empty;
                var sender = GetString(json, "sender", "Sender") ?? string.Empty;
                var senderDisplayName = GetString(json, "senderDisplayName", "SenderDisplayName") ?? sender;
                var content = GetString(json, "content", "Content") ?? string.Empty;
                var avatarUrl = GetString(json, "avatarUrl", "AvatarUrl");

                DateTime sentAt = DateTime.UtcNow;
                if (json.TryGetProperty("sentAt", out var sa) || json.TryGetProperty("SentAt", out sa))
                {
                    if (sa.ValueKind == JsonValueKind.String && DateTime.TryParse(sa.GetString(), out var parsed))
                        sentAt = parsed;
                }

                var msg = new ChatMessageDto(id, requestId, sender, senderDisplayName, content, sentAt, avatarUrl);
                RequestMessageReceived?.Invoke(msg);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse ReceiveRequestMessage payload.");
            }
        });

        _connection.On<Guid>("CraftingRequestCreated", id =>
        {
            CraftingRequestCreated?.Invoke(id);
        });

        _connection.On<Guid>("CraftingRequestUpdated", id =>
        {
            CraftingRequestUpdated?.Invoke(id);
        });

        _connection.On<Guid>("AssistanceRequestCreated", id =>
        {
            AssistanceRequestCreated?.Invoke(id);
        });

        _connection.On<Guid>("AssistanceRequestUpdated", id =>
        {
            AssistanceRequestUpdated?.Invoke(id);
        });

        _connection.On<JsonElement>("PresenceUpdated", json =>
        {
            try
            {
                var users = new List<OnlineUserDto>();

                var array = json.ValueKind == JsonValueKind.Array
                    ? json.EnumerateArray()
                    : default;

                foreach (var item in array)
                {
                    var username = GetString(item, "username", "Username") ?? string.Empty;
                    var displayName = GetString(item, "displayName", "DisplayName") ?? username;
                    var role = GetString(item, "role", "Role");
                    var avatarUrl = GetString(item, "avatarUrl", "AvatarUrl");

                    users.Add(new OnlineUserDto(username, displayName, role, avatarUrl));
                }

                PresenceUpdated?.Invoke(users.AsReadOnly());
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse PresenceUpdated payload.");
            }
        });

        // ── Connection state callbacks ─────────────────────────────────
        _connection.Reconnected += _ =>
        {
            ConnectionStateChanged?.Invoke(true);
            return Task.CompletedTask;
        };

        _connection.Closed += _ =>
        {
            ConnectionStateChanged?.Invoke(false);
            return Task.CompletedTask;
        };

        try
        {
            await _connection.StartAsync();
            ConnectionStateChanged?.Invoke(true);
            _logger.LogInformation("SignalR hub connected to {Url}", hubUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to SignalR hub at {Url}", hubUrl);
            ConnectionStateChanged?.Invoke(false);
        }
    }

    /// <summary>
    /// Joins a lobby channel group and requests its message history.
    /// </summary>
    public async Task JoinChannelAsync(string channel)
    {
        if (!IsConnected) return;
        try
        {
            await _connection!.InvokeAsync("JoinLobby", channel);
            await _connection!.InvokeAsync("GetLobbyHistory", channel);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to join channel '{Channel}'.", channel);
        }
    }

    /// <summary>
    /// Sends a message to the specified lobby channel.
    /// </summary>
    public async Task SendMessageAsync(string channel, string content)
    {
        if (!IsConnected || string.IsNullOrWhiteSpace(content)) return;
        try
        {
            await _connection!.InvokeAsync("SendLobbyMessage", channel, content);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send message to channel '{Channel}'.", channel);
        }
    }

    /// <summary>
    /// Joins a request-specific SignalR room.
    /// </summary>
    public async Task JoinRequestRoomAsync(string requestId)
    {
        if (!IsConnected) return;
        try
        {
            await _connection!.InvokeAsync("JoinRequestRoom", requestId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to join request room '{RequestId}'.", requestId);
        }
    }

    /// <summary>
    /// Leaves a request-specific SignalR room.
    /// </summary>
    public async Task LeaveRequestRoomAsync(string requestId)
    {
        if (!IsConnected) return;
        try
        {
            await _connection!.InvokeAsync("LeaveRequestRoom", requestId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to leave request room '{RequestId}'.", requestId);
        }
    }

    /// <summary>
    /// Sends a message to a request-specific room.
    /// </summary>
    public async Task SendRequestMessageAsync(string requestId, string content)
    {
        if (!IsConnected || string.IsNullOrWhiteSpace(content)) return;
        try
        {
            await _connection!.InvokeAsync("SendRequestMessage", requestId, content);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send message to request room '{RequestId}'.", requestId);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection != null)
        {
            try { await _connection.DisposeAsync(); }
            catch { /* suppress disposal errors */ }
        }
    }

    // ── Private helpers ───────────────────────────────────────────────

    private static ChatMessageDto? ParseChatMessage(JsonElement json, string? fallbackChannel = null)
    {
        var id = GetString(json, "id", "Id") ?? Guid.NewGuid().ToString();
        var channel = GetString(json, "channel", "Channel") ?? fallbackChannel ?? string.Empty;
        var sender = GetString(json, "sender", "Sender") ?? string.Empty;
        var senderDisplayName = GetString(json, "senderDisplayName", "SenderDisplayName") ?? sender;
        var content = GetString(json, "content", "Content") ?? string.Empty;
        var avatarUrl = GetString(json, "avatarUrl", "AvatarUrl");

        DateTime sentAt = DateTime.UtcNow;
        if (json.TryGetProperty("sentAt", out var sa) || json.TryGetProperty("SentAt", out sa))
        {
            if (sa.ValueKind == JsonValueKind.String && DateTime.TryParse(sa.GetString(), out var parsed))
                sentAt = parsed;
        }

        return new ChatMessageDto(id, channel, sender, senderDisplayName, content, sentAt, avatarUrl);
    }

    private static string? GetString(JsonElement el, string camelKey, string pascalKey)
    {
        if (el.TryGetProperty(camelKey, out var val) || el.TryGetProperty(pascalKey, out val))
            return val.GetString();
        return null;
    }
}

/// <summary>
/// Represents a chat message received from the SignalR hub.
/// </summary>
public record ChatMessageDto(
    string Id,
    string Channel,
    string Sender,
    string SenderDisplayName,
    string Content,
    DateTime SentAt,
    string? AvatarUrl);

/// <summary>
/// Represents an online user entry received from the SignalR presence system.
/// </summary>
public record OnlineUserDto(
    string Username,
    string DisplayName,
    string? Role,
    string? AvatarUrl);

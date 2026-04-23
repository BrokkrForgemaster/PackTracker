using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
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
    private readonly AuthTokenService _authTokenService;
    private readonly ILogger<SignalRChatService> _logger;
    private readonly HashSet<string> _joinedChannels = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _joinedRequestRooms = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _onlineUsers = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? _heartbeatCts;

    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromMinutes(2);

    // Events the ViewModel subscribes to
    public event Action<ChatMessageDto>? MessageReceived;
    public event Action<ChatMessageDto>? RequestMessageReceived;
    public event Action<ChatMessageEditedDto>? MessageEdited;
    public event Action<ChatMessageDeletedDto>? MessageDeleted;
    public event Action<IReadOnlyList<OnlineUserDto>>? PresenceUpdated;
    public event Action<bool>? ConnectionStateChanged;
    public event Action<Guid>? CraftingRequestCreated;
    public event Action<Guid>? CraftingRequestUpdated;
    public event Action<Guid>? ProcurementRequestCreated;
    public event Action<Guid>? ProcurementRequestUpdated;
    public event Action<Guid>? AssistanceRequestCreated;
    public event Action<Guid>? AssistanceRequestUpdated;
    public event Action<ClaimNotificationDto>? RequestClaimed;
    public event Action<ClaimNotificationDto>? ClaimConfirmed;
    public event Action<IReadOnlyList<PendingDmDto>>? PendingDirectMessages;

    public bool IsConnected => _connection?.State == HubConnectionState.Connected;

    public SignalRChatService(
        ISettingsService settingsService,
        AuthTokenService authTokenService,
        ILogger<SignalRChatService> logger)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _authTokenService = authTokenService ?? throw new ArgumentNullException(nameof(authTokenService));
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
        if (string.IsNullOrWhiteSpace(settings.ApiBaseUrl))
            throw new InvalidOperationException("API base URL is not configured.");

        var baseUrl = settings.ApiBaseUrl.TrimEnd('/');
        var hubUrl = $"{baseUrl}/hubs/requests";

        _connection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.AccessTokenProvider = () => _authTokenService.GetAccessTokenAsync();
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

        _connection.On<JsonElement>("ReceiveDirectMessage", json =>
        {
            try
            {
                var counterpart = GetString(json, "counterpartUsername", "CounterpartUsername");
                var fallbackChannel = !string.IsNullOrWhiteSpace(counterpart)
                    ? $"direct:{counterpart.Trim().ToLowerInvariant()}"
                    : GetString(json, "channel", "Channel");

                var msg = ParseChatMessage(json, fallbackChannel);
                if (msg != null)
                    MessageReceived?.Invoke(msg);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse ReceiveDirectMessage payload.");
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
                    if (sa.ValueKind == JsonValueKind.String &&
                        DateTime.TryParse(sa.GetString(),
                            System.Globalization.CultureInfo.InvariantCulture,
                            System.Globalization.DateTimeStyles.RoundtripKind,
                            out var parsed))
                        sentAt = parsed.Kind == DateTimeKind.Unspecified
                            ? DateTime.SpecifyKind(parsed, DateTimeKind.Utc)
                            : parsed;
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

        _connection.On<Guid>("ProcurementRequestCreated", id =>
        {
            ProcurementRequestCreated?.Invoke(id);
        });

        _connection.On<Guid>("ProcurementUpdated", id =>
        {
            ProcurementRequestUpdated?.Invoke(id);
        });

        _connection.On<Guid>("AssistanceRequestCreated", id =>
        {
            AssistanceRequestCreated?.Invoke(id);
        });

        _connection.On<Guid>("AssistanceRequestUpdated", id =>
        {
            AssistanceRequestUpdated?.Invoke(id);
        });

        _connection.On<JsonElement>("RequestClaimed", json =>
        {
            try
            {
                var dto = ParseClaimNotification(json);
                if (dto != null) RequestClaimed?.Invoke(dto);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse RequestClaimed payload.");
            }
        });

        _connection.On<JsonElement>("ClaimConfirmed", json =>
        {
            try
            {
                var dto = ParseClaimNotification(json);
                if (dto != null) ClaimConfirmed?.Invoke(dto);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse ClaimConfirmed payload.");
            }
        });

        _connection.On<JsonElement>("PendingDirectMessages", json =>
        {
            try
            {
                var dms = new List<PendingDmDto>();
                foreach (var item in json.EnumerateArray())
                {
                    var channel = GetString(item, "channel", "Channel") ?? string.Empty;
                    var lastSenderUsername = GetString(item, "lastSenderUsername", "LastSenderUsername") ?? string.Empty;
                    var lastSenderDisplayName = GetString(item, "lastSenderDisplayName", "LastSenderDisplayName") ?? string.Empty;
                    int unreadCount = 0;
                    if (item.TryGetProperty("unreadCount", out var uc) || item.TryGetProperty("UnreadCount", out uc))
                        unreadCount = uc.GetInt32();
                    if (!string.IsNullOrWhiteSpace(channel))
                        dms.Add(new PendingDmDto(channel, unreadCount, lastSenderUsername, lastSenderDisplayName));
                }
                if (dms.Count > 0)
                    PendingDirectMessages?.Invoke(dms.AsReadOnly());
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse PendingDirectMessages payload.");
            }
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

                _onlineUsers.Clear();
                foreach (var user in users.Where(x => !string.IsNullOrWhiteSpace(x.Username)))
                    _onlineUsers.Add(user.Username);

                PresenceUpdated?.Invoke(users.AsReadOnly());
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse PresenceUpdated payload.");
            }
        });

        _connection.On<JsonElement>("LobbyMessageEdited", json =>
        {
            try
            {
                var messageId = GetString(json, "messageId", "MessageId") ?? string.Empty;
                var channel = GetString(json, "channel", "Channel") ?? string.Empty;
                var newContent = GetString(json, "newContent", "NewContent") ?? string.Empty;
                DateTime editedAt = DateTime.UtcNow;
                if (json.TryGetProperty("editedAt", out var ea) || json.TryGetProperty("EditedAt", out ea))
                {
                    if (ea.ValueKind == JsonValueKind.String && DateTime.TryParse(ea.GetString(), out var parsed))
                        editedAt = parsed;
                }
                MessageEdited?.Invoke(new ChatMessageEditedDto(messageId, channel, newContent, editedAt));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse LobbyMessageEdited payload.");
            }
        });

        _connection.On<JsonElement>("LobbyMessageDeleted", json =>
        {
            try
            {
                var messageId = GetString(json, "messageId", "MessageId") ?? string.Empty;
                var channel = GetString(json, "channel", "Channel") ?? string.Empty;
                MessageDeleted?.Invoke(new ChatMessageDeletedDto(messageId, channel));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse LobbyMessageDeleted payload.");
            }
        });

        // ── Connection state callbacks ─────────────────────────────────
        _connection.Reconnected += connectionId =>
        {
            _ = RestoreSubscriptionsAsync();
            StartHeartbeat();
            ConnectionStateChanged?.Invoke(true);
            return Task.CompletedTask;
        };

        _connection.Closed += _ =>
        {
            StopHeartbeat();
            ConnectionStateChanged?.Invoke(false);
            return Task.CompletedTask;
        };

        try
        {
            await _connection.StartAsync();
            StartHeartbeat();
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
            _joinedChannels.Add(channel);
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

    public async Task SendDirectMessageAsync(string username, string content)
    {
        if (!IsConnected || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(content))
            return;

        try
        {
            await _connection!.InvokeAsync("SendDirectMessage", username, content);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send direct message to '{Username}'.", username);
        }
    }

    public async Task GetDirectMessageHistoryAsync(string targetUsername)
    {
        if (!IsConnected) return;
        try
        {
            await _connection!.InvokeAsync("GetDirectMessageHistory", targetUsername);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get DM history for '{Username}'.", targetUsername);
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
            _joinedRequestRooms.Add(requestId);
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
            _joinedRequestRooms.Remove(requestId);
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

    /// <summary>
    /// Edits a previously sent lobby message.
    /// </summary>
    public async Task EditMessageAsync(string channel, string messageId, string newContent)
    {
        if (!IsConnected || string.IsNullOrWhiteSpace(newContent)) return;
        try
        {
            await _connection!.InvokeAsync("EditLobbyMessage", channel, messageId, newContent);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to edit message '{MessageId}' in channel '{Channel}'.", messageId, channel);
        }
    }

    /// <summary>
    /// Deletes a previously sent lobby message.
    /// </summary>
    public async Task DeleteMessageAsync(string channel, string messageId)
    {
        if (!IsConnected) return;
        try
        {
            await _connection!.InvokeAsync("DeleteLobbyMessage", channel, messageId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete message '{MessageId}' in channel '{Channel}'.", messageId, channel);
        }
    }

    public async ValueTask DisposeAsync()
    {
        StopHeartbeat();

        if (_connection != null)
        {
            try { await _connection.DisposeAsync(); }
            catch { /* suppress disposal errors */ }
        }
    }

    // ── Private helpers ───────────────────────────────────────────────

    private void StartHeartbeat()
    {
        StopHeartbeat();
        _heartbeatCts = new CancellationTokenSource();
        _ = RunHeartbeatAsync(_heartbeatCts.Token);
    }

    private void StopHeartbeat()
    {
        if (_heartbeatCts == null)
            return;

        try
        {
            _heartbeatCts.Cancel();
            _heartbeatCts.Dispose();
        }
        catch
        {
        }
        finally
        {
            _heartbeatCts = null;
        }
    }

    public bool IsUserOnline(string? username) =>
        !string.IsNullOrWhiteSpace(username) && _onlineUsers.Contains(username);

    public void UpdateOnlineUsersSnapshot(IEnumerable<string> usernames)
    {
        _onlineUsers.Clear();
        foreach (var username in usernames.Where(x => !string.IsNullOrWhiteSpace(x)))
            _onlineUsers.Add(username);
    }

    private async Task RunHeartbeatAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(HeartbeatInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                if (!IsConnected)
                    continue;

                await _connection!.InvokeAsync("Heartbeat", cancellationToken: ct);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "SignalR heartbeat failed.");
        }
    }

    private async Task RestoreSubscriptionsAsync()
    {
        if (!IsConnected)
            return;

        foreach (var channel in _joinedChannels.ToArray())
        {
            await JoinChannelAsync(channel);
        }

        foreach (var requestId in _joinedRequestRooms.ToArray())
        {
            await JoinRequestRoomAsync(requestId);
        }
    }

    private static ChatMessageDto? ParseChatMessage(JsonElement json, string? fallbackChannel = null)
    {
        var id = GetString(json, "id", "Id") ?? Guid.NewGuid().ToString();
        var channel = GetString(json, "channel", "Channel") ?? fallbackChannel ?? string.Empty;
        var sender = GetString(json, "sender", "Sender") ?? string.Empty;
        var senderDisplayName = GetString(json, "senderDisplayName", "SenderDisplayName") ?? sender;
        var content = GetString(json, "content", "Content") ?? string.Empty;
        var avatarUrl = GetString(json, "avatarUrl", "AvatarUrl");
        var senderRole = GetString(json, "senderRole", "SenderRole");

        DateTime sentAt = DateTime.UtcNow;
        if (json.TryGetProperty("sentAt", out var sa) || json.TryGetProperty("SentAt", out sa))
        {
            if (sa.ValueKind == JsonValueKind.String &&
                DateTime.TryParse(sa.GetString(),
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.RoundtripKind,
                    out var parsed))
                sentAt = parsed.Kind == DateTimeKind.Unspecified
                    ? DateTime.SpecifyKind(parsed, DateTimeKind.Utc)
                    : parsed;
        }

        return new ChatMessageDto(id, channel, sender, senderDisplayName, content, sentAt, avatarUrl, senderRole);
    }

    private static string? GetString(JsonElement el, string camelKey, string pascalKey)
    {
        if (el.TryGetProperty(camelKey, out var val) || el.TryGetProperty(pascalKey, out val))
            return val.GetString();
        return null;
    }

    private static ClaimNotificationDto? ParseClaimNotification(JsonElement json)
    {
        var requestIdStr = GetString(json, "requestId", "RequestId");
        if (!Guid.TryParse(requestIdStr, out var requestId))
            return null;

        var requestType = GetString(json, "requestType", "RequestType") ?? string.Empty;
        var requestLabel = GetString(json, "requestLabel", "RequestLabel") ?? string.Empty;
        var claimerDisplayName = GetString(json, "claimerDisplayName", "ClaimerDisplayName") ?? string.Empty;
        var requesterDisplayName = GetString(json, "requesterDisplayName", "RequesterDisplayName") ?? string.Empty;

        return new ClaimNotificationDto(requestId, requestType, requestLabel, claimerDisplayName, requesterDisplayName);
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
    string? AvatarUrl,
    string? SenderRole = null);

public record ChatMessageEditedDto(string MessageId, string Channel, string NewContent, DateTime EditedAt);
public record ChatMessageDeletedDto(string MessageId, string Channel);

public record ClaimNotificationDto(
    Guid RequestId,
    string RequestType,
    string RequestLabel,
    string ClaimerDisplayName,
    string RequesterDisplayName);

public record PendingDmDto(
    string Channel,
    int UnreadCount,
    string LastSenderUsername,
    string LastSenderDisplayName);

/// <summary>
/// Represents an online user entry received from the SignalR presence system.
/// </summary>
public record OnlineUserDto(
    string Username,
    string DisplayName,
    string? Role,
    string? AvatarUrl);

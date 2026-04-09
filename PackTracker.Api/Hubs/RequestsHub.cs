using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using PackTracker.Application.Interfaces;

namespace PackTracker.Api.Hubs;

/// <summary>
/// SignalR hub used for realtime request updates, request-specific rooms,
/// operational lobbies, presence tracking, and role-based chat channels.
/// </summary>
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class RequestsHub : Hub
{
    #region Constants

    /// <summary>
    /// The route used to map the hub in the API.
    /// </summary>
    public const string Route = "/hubs/requests";

    private const int MaxHistoryPerChannel = 50;

    #endregion

    #region Static State (survives hub instance recreation)

    // SignalR creates a new hub instance per invocation; static fields persist across calls.
    private static readonly ConcurrentDictionary<string, Queue<ChatMessage>> _messageHistory = new();
    private static readonly ConcurrentDictionary<string, string> _connectionRegistry = new(); // connectionId → discordId

    #endregion

    #region Dependencies

    private readonly IProfileService _profiles;

    #endregion

    #region Constructor

    public RequestsHub(IProfileService profiles)
    {
        _profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
    }

    #endregion

    #region Connection Lifecycle

    /// <summary>
    /// Called when a client connects to the hub.
    /// Marks the user online and broadcasts presence to all clients.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var discordId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        var username = Context.User?.Identity?.Name ?? "Unknown";
        var connectionId = Context.ConnectionId;

        if (!string.IsNullOrWhiteSpace(discordId))
        {
            _connectionRegistry[connectionId] = discordId;
            await _profiles.MarkOnlineAsync(discordId, CancellationToken.None);
            await BroadcastPresenceAsync();
        }

        await Clients.Caller.SendAsync("Connected", new
        {
            ConnectionId = connectionId,
            Username = username,
            ConnectedAt = DateTime.UtcNow
        });

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Called when a client disconnects from the hub.
    /// Marks the user offline and broadcasts updated presence.
    /// </summary>
    /// <param name="exception">The exception that caused the disconnect, if any.</param>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var connectionId = Context.ConnectionId;

        if (_connectionRegistry.TryRemove(connectionId, out var discordId))
        {
            // Only mark offline if no other active connections remain for this discord account
            var hasOtherConnections = _connectionRegistry.Values.Any(id => id == discordId);
            if (!hasOtherConnections)
            {
                await _profiles.MarkOfflineAsync(discordId, CancellationToken.None);
            }

            await BroadcastPresenceAsync();
        }

        await base.OnDisconnectedAsync(exception);
    }

    #endregion

    #region Chat

    /// <summary>
    /// Broadcasts a message to all members of a lobby channel and stores it in history.
    /// </summary>
    /// <param name="lobbyName">The target lobby channel name.</param>
    /// <param name="content">The message content.</param>
    public async Task SendLobbyMessage(string lobbyName, string content)
    {
        if (string.IsNullOrWhiteSpace(lobbyName))
            throw new HubException("Lobby name is required.");

        if (string.IsNullOrWhiteSpace(content))
            throw new HubException("Message content cannot be empty.");

        var discordId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var username = Context.User?.Identity?.Name ?? "Unknown";

        string senderDisplayName = username;
        string? avatarUrl = null;

        try
        {
            var profile = await _profiles.GetByDiscordIdAsync(discordId, CancellationToken.None);
            if (profile != null)
            {
                senderDisplayName = profile.DiscordDisplayName ?? profile.Username;
                avatarUrl = profile.DiscordAvatarUrl;
            }
        }
        catch
        {
            // Non-fatal: fall back to username
        }

        var normalizedLobby = NormalizeLobbyName(lobbyName);

        var message = new ChatMessage(
            Id: Guid.NewGuid().ToString(),
            Sender: username,
            SenderDisplayName: senderDisplayName,
            Content: content.Trim(),
            SentAt: DateTime.UtcNow,
            SenderDiscordId: discordId,
            AvatarUrl: avatarUrl);

        // Store in capped history queue
        var queue = _messageHistory.GetOrAdd(normalizedLobby, _ => new Queue<ChatMessage>());
        lock (queue)
        {
            queue.Enqueue(message);
            while (queue.Count > MaxHistoryPerChannel)
                queue.Dequeue();
        }

        // Broadcast to lobby group
        await Clients.Group(normalizedLobby).SendAsync("ReceiveLobbyMessage", new
        {
            message.Id,
            Channel = lobbyName,
            message.Sender,
            message.SenderDisplayName,
            message.Content,
            message.SentAt,
            message.AvatarUrl
        });
    }

    /// <summary>
    /// Sends the message history for a given lobby channel to the calling client.
    /// </summary>
    /// <param name="lobbyName">The lobby channel name.</param>
    public async Task GetLobbyHistory(string lobbyName)
    {
        if (string.IsNullOrWhiteSpace(lobbyName))
            throw new HubException("Lobby name is required.");

        var normalizedLobby = NormalizeLobbyName(lobbyName);

        ChatMessage[] history;
        if (_messageHistory.TryGetValue(normalizedLobby, out var queue))
        {
            lock (queue)
            {
                history = queue.ToArray();
            }
        }
        else
        {
            history = Array.Empty<ChatMessage>();
        }

        await Clients.Caller.SendAsync("LobbyHistory", new
        {
            Channel = lobbyName,
            Messages = history.Select(m => new
            {
                m.Id,
                Channel = lobbyName,
                m.Sender,
                m.SenderDisplayName,
                m.Content,
                m.SentAt,
                m.AvatarUrl
            })
        });
    }

    #endregion

    #region Request Rooms

    /// <summary>
    /// Adds the current connection to a request-specific SignalR group.
    /// </summary>
    /// <param name="requestId">The request identifier.</param>
    public async Task JoinRequestRoom(string requestId)
    {
        var groupName = GetRequestGroupName(requestId);
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

        await Clients.Caller.SendAsync("JoinedRequestRoom", new
        {
            RequestId = requestId,
            Group = groupName
        });
    }

    /// <summary>
    /// Removes the current connection from a request-specific SignalR group.
    /// </summary>
    /// <param name="requestId">The request identifier.</param>
    public async Task LeaveRequestRoom(string requestId)
    {
        var groupName = GetRequestGroupName(requestId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);

        await Clients.Caller.SendAsync("LeftRequestRoom", new
        {
            RequestId = requestId,
            Group = groupName
        });
    }

    /// <summary>
    /// Sends a message to all members of a request-specific room.
    /// </summary>
    public async Task SendRequestMessage(string requestId, string content)
    {
        if (string.IsNullOrWhiteSpace(requestId))
            throw new HubException("Request ID is required.");
        if (string.IsNullOrWhiteSpace(content))
            throw new HubException("Message content cannot be empty.");

        var discordId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var username = Context.User?.Identity?.Name ?? "Unknown";

        string senderDisplayName = username;
        string? avatarUrl = null;

        try
        {
            var profile = await _profiles.GetByDiscordIdAsync(discordId, CancellationToken.None);
            if (profile != null)
            {
                senderDisplayName = profile.DiscordDisplayName ?? profile.Username;
                avatarUrl = profile.DiscordAvatarUrl;
            }
        }
        catch { /* Non-fatal */ }

        var groupName = GetRequestGroupName(requestId);

        var message = new ChatMessage(
            Id: Guid.NewGuid().ToString(),
            Sender: username,
            SenderDisplayName: senderDisplayName,
            Content: content.Trim(),
            SentAt: DateTime.UtcNow,
            SenderDiscordId: discordId,
            AvatarUrl: avatarUrl);

        await Clients.Group(groupName).SendAsync("ReceiveRequestMessage", new
        {
            message.Id,
            RequestId = requestId,
            message.Sender,
            message.SenderDisplayName,
            message.Content,
            message.SentAt,
            message.AvatarUrl
        });
    }

    #endregion

    #region Lobby Rooms

    /// <summary>
    /// Adds the current connection to a named lobby group.
    /// </summary>
    /// <param name="lobbyName">The lobby name.</param>
    public async Task JoinLobby(string lobbyName)
    {
        if (string.IsNullOrWhiteSpace(lobbyName))
            throw new HubException("Lobby name is required.");

        var normalizedLobby = NormalizeLobbyName(lobbyName);

        await Groups.AddToGroupAsync(Context.ConnectionId, normalizedLobby);

        await Clients.Caller.SendAsync("JoinedLobby", new
        {
            Lobby = normalizedLobby
        });
    }

    /// <summary>
    /// Removes the current connection from a named lobby group.
    /// </summary>
    /// <param name="lobbyName">The lobby name.</param>
    public async Task LeaveLobby(string lobbyName)
    {
        if (string.IsNullOrWhiteSpace(lobbyName))
            throw new HubException("Lobby name is required.");

        var normalizedLobby = NormalizeLobbyName(lobbyName);

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, normalizedLobby);

        await Clients.Caller.SendAsync("LeftLobby", new
        {
            Lobby = normalizedLobby
        });
    }

    #endregion

    #region Diagnostics

    /// <summary>
    /// Simple connectivity test method for verifying SignalR communication.
    /// </summary>
    public async Task Ping()
    {
        await Clients.Caller.SendAsync("Pong", new
        {
            ServerTimeUtc = DateTime.UtcNow,
            ConnectionId = Context.ConnectionId
        });
    }

    #endregion

    #region Private Helpers

    /// <summary>
    /// Fetches all currently-online profiles and broadcasts the presence list to all connected clients.
    /// </summary>
    private async Task BroadcastPresenceAsync()
    {
        try
        {
            var onlineProfiles = await _profiles.GetOnlineAsync(CancellationToken.None);

            var presenceList = onlineProfiles.Select(p => new
            {
                Username = p.Username,
                DisplayName = p.DiscordDisplayName ?? p.Username,
                Role = p.DiscordRank,
                AvatarUrl = p.DiscordAvatarUrl
            });

            await Clients.All.SendAsync("PresenceUpdated", presenceList);
        }
        catch
        {
            // Non-fatal — presence broadcast failure must not interrupt connect/disconnect flow
        }
    }

    /// <summary>
    /// Builds the SignalR group name for a request-specific room.
    /// </summary>
    private static string GetRequestGroupName(string requestId) =>
        $"request:{requestId.Trim()}";

    /// <summary>
    /// Normalizes a lobby name into a safe SignalR group key.
    /// </summary>
    private static string NormalizeLobbyName(string lobbyName) =>
        $"lobby:{lobbyName.Trim().ToLowerInvariant()}";

    #endregion

    #region Private Records

    /// <summary>
    /// Represents a stored chat message in the in-memory history buffer.
    /// </summary>
    private record ChatMessage(
        string Id,
        string Sender,
        string SenderDisplayName,
        string Content,
        DateTime SentAt,
        string SenderDiscordId,
        string? AvatarUrl);

    #endregion
}

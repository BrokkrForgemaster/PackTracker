using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Entities;
using PackTracker.Infrastructure.Persistence;

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

    private static readonly ChatMessage _generalWelcomeMessage = new(
        Id: "system-welcome-001",
        Sender: "PackTracker",
        SenderDisplayName: "PackTracker",
        Content:
            "═══════════════════════════════════════\n" +
            "  WELCOME TO PACKTRACKER — HOUSE WOLF OPERATIONS HUB\n" +
            "═══════════════════════════════════════\n\n" +
            "PackTracker is your all-in-one command center for coordinating House Wolf operations in Star Citizen.\n\n" +
            "[ ACTIVE REQUESTS DASHBOARD ]\n" +
            "Your landing view shows every open Assistance, Crafting, and Procurement request across the org in real time. Pin high-priority items, claim requests assigned to you, and track status through the pipeline.\n\n" +
            "[ CRAFTING CENTER ]\n" +
            "Submit crafting requests for blueprinted items and get matched with org crafters. Crafters can claim, accept, refuse, or complete requests and communicate inside the request's live chat thread.\n\n" +
            "[ PROCUREMENT QUEUE ]\n" +
            "Need raw materials sourced? Drop a procurement request and let logistics members pick it up. Full status tracking from open through delivery.\n\n" +
            "[ ASSISTANCE REQUESTS ]\n" +
            "Need backup, a ride, or situational help in the verse? Open an assistance request and any available org member can claim it and coordinate through the built-in request chat.\n\n" +
            "[ BLUEPRINT EXPLORER ]\n" +
            "Browse the full House Wolf blueprint library — filter by category, search by name, and view full material breakdowns before committing to a crafting request.\n\n" +
            "[ TRADING HUB (UEX) ]\n" +
            "Live commodity pricing and trade route optimization powered by UEX data. Find the best buy/sell locations and calculate optimal cargo runs for your ship.\n\n" +
            "[ GUIDE SCHEDULING ]\n" +
            "Leadership can schedule and track guided sessions — tours, training runs, and org events — visible directly on the dashboard.\n\n" +
            "[ LIVE CHAT & DIVISION CHANNELS ]\n" +
            "Real-time org-wide chat right here in General. Members with division roles (LOCOPS, TACOPS, SPECOPS, ARCOPS, Leadership) get access to their division channels — automatically unlocked based on your Discord roles.\n\n" +
            "[ PRESENCE & ONLINE STATUS ]\n" +
            "See who's online in the org at a glance from the sidebar. Direct message any online member without leaving the app.\n\n" +
            "═══════════════════════════════════════\n" +
            "  Log in with Discord to get started. Division channels unlock automatically after your first login.\n" +
            "═══════════════════════════════════════",
        SentAt: new DateTime(2026, 4, 19, 0, 0, 0, DateTimeKind.Utc),
        SenderDiscordId: "system",
        AvatarUrl: null,
        SenderRole: "System");

    #endregion

    #region Dependencies

    private readonly IProfileService _profiles;
    private readonly AppDbContext _db;

    #endregion

    #region Constructor

    public RequestsHub(IProfileService profiles, AppDbContext db)
    {
        _profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
        _db = db ?? throw new ArgumentNullException(nameof(db));
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

        string? senderRole = null;

        try
        {
            var profile = await _profiles.GetByDiscordIdAsync(discordId, CancellationToken.None);
            if (profile != null)
            {
                senderDisplayName = profile.DiscordDisplayName ?? profile.Username;
                avatarUrl = profile.DiscordAvatarUrl;
                senderRole = profile.DiscordRank;
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
            AvatarUrl: avatarUrl,
            SenderRole: senderRole);

        // Persist to database
        _db.LobbyChatMessages.Add(new LobbyChatMessage
        {
            Id = message.Id,
            Channel = normalizedLobby,
            Sender = message.Sender,
            SenderDisplayName = message.SenderDisplayName,
            Content = message.Content,
            SentAt = message.SentAt,
            SenderDiscordId = message.SenderDiscordId,
            AvatarUrl = message.AvatarUrl,
            SenderRole = message.SenderRole
        });
        await _db.SaveChangesAsync(CancellationToken.None);

        // Also keep in-memory cache for fast history within the same process lifetime
        StoreMessageHistory(normalizedLobby, message);

        // Broadcast to lobby group (exclude sender — they add the message locally for instant feedback)
        await Clients.Group(normalizedLobby).SendAsync("ReceiveLobbyMessage", new
        {
            message.Id,
            Channel = lobbyName,
            message.Sender,
            message.SenderDisplayName,
            message.Content,
            message.SentAt,
            message.AvatarUrl,
            message.SenderRole
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

        // Load from DB — most recent 100 messages, oldest first
        var dbMessages = await _db.LobbyChatMessages
            .Where(m => m.Channel == normalizedLobby && !m.IsDeleted)
            .OrderByDescending(m => m.SentAt)
            .Take(100)
            .OrderBy(m => m.SentAt)
            .ToListAsync();

        // Seed in-memory cache from DB so live edits/deletes within this process stay consistent
        if (dbMessages.Count > 0)
        {
            var q = _messageHistory.GetOrAdd(normalizedLobby, _ => new Queue<ChatMessage>());
            lock (q)
            {
                q.Clear();
                foreach (var row in dbMessages)
                    q.Enqueue(new ChatMessage(row.Id, row.Sender, row.SenderDisplayName,
                        row.Content, row.SentAt, row.SenderDiscordId, row.AvatarUrl, row.SenderRole));
            }
        }

        IEnumerable<object> messages = dbMessages.Select(m => (object)new
        {
            m.Id,
            Channel = lobbyName,
            m.Sender,
            m.SenderDisplayName,
            Content = m.EditedAt.HasValue ? m.Content : m.Content,
            m.SentAt,
            m.AvatarUrl,
            m.SenderRole
        });

        if (normalizedLobby == "lobby:general")
        {
            messages = new object[]
            {
                new
                {
                    _generalWelcomeMessage.Id,
                    Channel = lobbyName,
                    _generalWelcomeMessage.Sender,
                    _generalWelcomeMessage.SenderDisplayName,
                    _generalWelcomeMessage.Content,
                    _generalWelcomeMessage.SentAt,
                    _generalWelcomeMessage.AvatarUrl,
                    _generalWelcomeMessage.SenderRole
                }
            }.Concat(messages);
        }

        await Clients.Caller.SendAsync("LobbyHistory", new
        {
            Channel = lobbyName,
            Messages = messages
        });
    }

    /// <summary>
    /// Edits an existing lobby message. Only the original sender may edit their message.
    /// </summary>
    public async Task EditLobbyMessage(string lobbyName, string messageId, string newContent)
    {
        if (string.IsNullOrWhiteSpace(lobbyName))
            throw new HubException("Lobby name is required.");
        if (string.IsNullOrWhiteSpace(messageId))
            throw new HubException("Message ID is required.");
        if (string.IsNullOrWhiteSpace(newContent))
            throw new HubException("New content cannot be empty.");

        var discordId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var normalizedLobby = NormalizeLobbyName(lobbyName);

        var trimmedContent = newContent.Trim();
        var editedAt = DateTime.UtcNow;

        // Persist edit to DB
        var dbMsg = await _db.LobbyChatMessages.FindAsync(messageId)
            ?? throw new HubException("Message not found.");
        if (dbMsg.SenderDiscordId != discordId)
            throw new HubException("You can only edit your own messages.");
        dbMsg.Content = trimmedContent;
        dbMsg.EditedAt = editedAt;
        await _db.SaveChangesAsync(CancellationToken.None);

        // Update in-memory cache
        if (_messageHistory.TryGetValue(normalizedLobby, out var queue))
        {
            lock (queue)
            {
                var msg = queue.FirstOrDefault(m => m.Id == messageId);
                if (msg != null)
                    msg.Content = trimmedContent;
            }
        }

        await Clients.Group(normalizedLobby).SendAsync("LobbyMessageEdited", new
        {
            MessageId = messageId,
            Channel = lobbyName,
            NewContent = trimmedContent,
            EditedAt = editedAt
        });
    }

    /// <summary>
    /// Deletes a lobby message. Only the original sender may delete their message.
    /// </summary>
    public async Task DeleteLobbyMessage(string lobbyName, string messageId)
    {
        if (string.IsNullOrWhiteSpace(lobbyName))
            throw new HubException("Lobby name is required.");
        if (string.IsNullOrWhiteSpace(messageId))
            throw new HubException("Message ID is required.");

        var discordId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var normalizedLobby = NormalizeLobbyName(lobbyName);

        // Soft-delete in DB
        var dbMsg = await _db.LobbyChatMessages.FindAsync(messageId)
            ?? throw new HubException("Message not found.");
        if (dbMsg.SenderDiscordId != discordId)
            throw new HubException("You can only delete your own messages.");
        dbMsg.IsDeleted = true;
        await _db.SaveChangesAsync(CancellationToken.None);

        // Remove from in-memory cache
        if (_messageHistory.TryGetValue(normalizedLobby, out var queue))
        {
            lock (queue)
            {
                var updated = new Queue<ChatMessage>(queue.Where(m => m.Id != messageId));
                queue.Clear();
                foreach (var m in updated)
                    queue.Enqueue(m);
            }
        }

        await Clients.Group(normalizedLobby).SendAsync("LobbyMessageDeleted", new
        {
            MessageId = messageId,
            Channel = lobbyName
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
        string? senderRole = null;

        try
        {
            var profile = await _profiles.GetByDiscordIdAsync(discordId, CancellationToken.None);
            if (profile != null)
            {
                senderDisplayName = profile.DiscordDisplayName ?? profile.Username;
                avatarUrl = profile.DiscordAvatarUrl;
                senderRole = profile.DiscordRank;
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
            AvatarUrl: avatarUrl,
            SenderRole: senderRole);

        if (Guid.TryParse(requestId, out var parsedRequestId))
        {
            var profile = await _profiles.GetByDiscordIdAsync(discordId, CancellationToken.None);
            if (profile != null)
            {
                _db.RequestComments.Add(new RequestComment
                {
                    RequestId = parsedRequestId,
                    AuthorProfileId = profile.Id,
                    Content = message.Content,
                    IsLiveChat = true,
                    CreatedAt = message.SentAt
                });

                await _db.SaveChangesAsync(CancellationToken.None);
            }
        }

        await Clients.Group(groupName).SendAsync("ReceiveRequestMessage", new
        {
            message.Id,
            RequestId = requestId,
            message.Sender,
            message.SenderDisplayName,
            message.Content,
            message.SentAt,
            message.AvatarUrl,
            message.SenderRole
        });
    }

    public async Task SendDirectMessage(string targetUsername, string content)
    {
        if (string.IsNullOrWhiteSpace(targetUsername))
            throw new HubException("Target username is required.");

        if (string.IsNullOrWhiteSpace(content))
            throw new HubException("Message content cannot be empty.");

        var senderDiscordId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var senderUsername = Context.User?.Identity?.Name ?? "Unknown";

        var senderProfile = await _profiles.GetByDiscordIdAsync(senderDiscordId, CancellationToken.None);
        var targetProfile = await _profiles.GetByNameAsync(targetUsername.Trim(), CancellationToken.None);

        if (targetProfile == null)
            throw new HubException($"User '{targetUsername}' was not found.");

        var senderDisplayName = senderProfile?.DiscordDisplayName ?? senderUsername;
        var senderAvatarUrl = senderProfile?.DiscordAvatarUrl;
        var senderRole = senderProfile?.DiscordRank;
        var targetDisplayName = targetProfile.DiscordDisplayName ?? targetProfile.Username;
        var timestamp = DateTime.UtcNow;
        var messageId = Guid.NewGuid().ToString();
        var trimmedContent = content.Trim();
        var senderChannel = BuildDirectChannelName(targetProfile.Username);
        var recipientChannel = BuildDirectChannelName(senderUsername);

        var message = new ChatMessage(
            Id: messageId,
            Sender: senderUsername,
            SenderDisplayName: senderDisplayName,
            Content: trimmedContent,
            SentAt: timestamp,
            SenderDiscordId: senderDiscordId,
            AvatarUrl: senderAvatarUrl,
            SenderRole: senderRole);

        StoreMessageHistory(senderChannel, message);
        StoreMessageHistory(recipientChannel, message);

        // Persist DM to DB (stored once under sender's channel key)
        _db.LobbyChatMessages.Add(new LobbyChatMessage
        {
            Id = message.Id,
            Channel = senderChannel,
            Sender = message.Sender,
            SenderDisplayName = message.SenderDisplayName,
            Content = message.Content,
            SentAt = message.SentAt,
            SenderDiscordId = message.SenderDiscordId,
            AvatarUrl = message.AvatarUrl,
            SenderRole = message.SenderRole
        });
        await _db.SaveChangesAsync(CancellationToken.None);

        // Send to the recipient
        await Clients.User(targetProfile.DiscordId).SendAsync("ReceiveDirectMessage", new
        {
            Id = messageId,
            Channel = recipientChannel,
            Sender = senderUsername,
            SenderDisplayName = senderDisplayName,
            Content = trimmedContent,
            SentAt = timestamp,
            AvatarUrl = senderAvatarUrl,
            SenderRole = senderRole,
            CounterpartUsername = senderUsername,
            CounterpartDisplayName = senderDisplayName
        });

        // Echo back to the sender (they do not add DMs locally — the echo is the authoritative copy)
        await Clients.Caller.SendAsync("ReceiveDirectMessage", new
        {
            Id = messageId,
            Channel = senderChannel,
            Sender = senderUsername,
            SenderDisplayName = senderDisplayName,
            Content = trimmedContent,
            SentAt = timestamp,
            AvatarUrl = senderAvatarUrl,
            SenderRole = senderRole,
            CounterpartUsername = targetProfile.Username,
            CounterpartDisplayName = targetDisplayName
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

    public async Task Heartbeat()
    {
        var discordId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(discordId))
            return;

        await _profiles.TouchLastSeenAsync(discordId, CancellationToken.None);
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

    private static string BuildDirectChannelName(string username) =>
        $"direct:{username.Trim().ToLowerInvariant()}";

    private static void StoreMessageHistory(string normalizedLobby, ChatMessage message)
    {
        var queue = _messageHistory.GetOrAdd(normalizedLobby, _ => new Queue<ChatMessage>());
        lock (queue)
        {
            queue.Enqueue(message);
            while (queue.Count > MaxHistoryPerChannel)
                queue.Dequeue();
        }
    }

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
        string? AvatarUrl,
        string? SenderRole)
    {
        public string Content { get; set; } = Content;
    }

    #endregion
}


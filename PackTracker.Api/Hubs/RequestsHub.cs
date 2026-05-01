using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Entities;
using PackTracker.Domain.Security;
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
        var discordId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? Context.User?.FindFirstValue("nameidentifier")
            ?? Context.User?.FindFirstValue("sub");
        var username = Context.User?.Identity?.Name
            ?? Context.User?.FindFirstValue("unique_name")
            ?? "Unknown";
        var connectionId = Context.ConnectionId;

        if (!string.IsNullOrWhiteSpace(discordId))
        {
            // Capture LastSeenAt before MarkOnlineAsync resets it — used to find unread DMs
            var previousProfile = await _profiles.GetByDiscordIdAsync(discordId, CancellationToken.None);
            var lastSeenBefore = previousProfile?.LastSeenAt ?? DateTime.UtcNow.AddDays(-1);

            _connectionRegistry[connectionId] = discordId;
            await _profiles.MarkOnlineAsync(discordId, CancellationToken.None);
            await BroadcastPresenceAsync();

            await NotifyPendingDirectMessagesAsync(username, lastSeenBefore);
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
        var currentUsername = Context.User?.Identity?.Name ?? string.Empty;

        // PRIVACY: Prevent users from requesting history of DM channels they don't belong to
        if (normalizedLobby.StartsWith("dm:", StringComparison.OrdinalIgnoreCase))
        {
            var parts = normalizedLobby.Split(':');
            var lowerUser = currentUsername.ToLowerInvariant();
            if (parts.Length < 3 || (parts[1] != lowerUser && parts[2] != lowerUser))
            {
                throw new HubException("Access Denied: You do not have permission to view this conversation.");
            }
        }

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
            var generalWelcomeMessage = BuildGeneralWelcomeMessage();
            messages = new object[]
            {
                new
                {
                    generalWelcomeMessage.Id,
                    Channel = lobbyName,
                    generalWelcomeMessage.Sender,
                    generalWelcomeMessage.SenderDisplayName,
                    generalWelcomeMessage.Content,
                    generalWelcomeMessage.SentAt,
                    generalWelcomeMessage.AvatarUrl,
                    generalWelcomeMessage.SenderRole
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

        await AuthorizeModerationByDiscordIdAsync(discordId, dbMsg.SenderDiscordId, "edit");

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

        await AuthorizeModerationByDiscordIdAsync(discordId, dbMsg.SenderDiscordId, "delete");

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
                    Id = Guid.Parse(message.Id),
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

    /// <summary>
    /// Edits an existing request-room message.
    /// </summary>
    public async Task EditRequestMessage(string requestId, string messageId, string newContent)
    {
        if (string.IsNullOrWhiteSpace(requestId))
            throw new HubException("Request ID is required.");
        if (string.IsNullOrWhiteSpace(messageId))
            throw new HubException("Message ID is required.");
        if (string.IsNullOrWhiteSpace(newContent))
            throw new HubException("New content cannot be empty.");

        var discordId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var trimmedContent = newContent.Trim();
        var editedAt = DateTime.UtcNow;

        if (!Guid.TryParse(messageId, out var commentId))
            throw new HubException("Invalid message ID.");

        var dbMsg = await _db.RequestComments.FindAsync(commentId)
            ?? throw new HubException("Message not found.");

        await AuthorizeModerationByProfileIdAsync(discordId, dbMsg.AuthorProfileId, "edit");

        dbMsg.Content = trimmedContent;
        dbMsg.EditedAt = editedAt;
        await _db.SaveChangesAsync(CancellationToken.None);

        var groupName = GetRequestGroupName(requestId);
        await Clients.Group(groupName).SendAsync("RequestMessageEdited", new
        {
            MessageId = messageId,
            RequestId = requestId,
            NewContent = trimmedContent,
            EditedAt = editedAt
        });
    }

    /// <summary>
    /// Deletes (soft-deletes) an existing request-room message.
    /// </summary>
    public async Task DeleteRequestMessage(string requestId, string messageId)
    {
        if (string.IsNullOrWhiteSpace(requestId))
            throw new HubException("Request ID is required.");
        if (string.IsNullOrWhiteSpace(messageId))
            throw new HubException("Message ID is required.");

        var discordId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

        if (!Guid.TryParse(messageId, out var commentId))
            throw new HubException("Invalid message ID.");

        var dbMsg = await _db.RequestComments.FindAsync(commentId)
            ?? throw new HubException("Message not found.");

        await AuthorizeModerationByProfileIdAsync(discordId, dbMsg.AuthorProfileId, "delete");

        dbMsg.IsDeleted = true;
        await _db.SaveChangesAsync(CancellationToken.None);

        var groupName = GetRequestGroupName(requestId);
        await Clients.Group(groupName).SendAsync("RequestMessageDeleted", new
        {
            MessageId = messageId,
            RequestId = requestId
        });
    }

    public async Task GetDirectMessageHistory(string targetUsername)
    {
        if (string.IsNullOrWhiteSpace(targetUsername))
            throw new HubException("Target username is required.");

        var currentUsername = Context.User?.Identity?.Name
            ?? Context.User?.FindFirstValue("unique_name")
            ?? string.Empty;
        var dmChannel = BuildPrivateChannelName(currentUsername, targetUsername.Trim());

        var dbMessages = await _db.LobbyChatMessages
            .Where(m => m.Channel == dmChannel && !m.IsDeleted)
            .OrderByDescending(m => m.SentAt)
            .Take(100)
            .OrderBy(m => m.SentAt)
            .ToListAsync();

        if (dbMessages.Count > 0)
        {
            var q = _messageHistory.GetOrAdd(dmChannel, _ => new Queue<ChatMessage>());
            lock (q)
            {
                q.Clear();
                foreach (var row in dbMessages)
                    q.Enqueue(new ChatMessage(row.Id, row.Sender, row.SenderDisplayName,
                        row.Content, row.SentAt, row.SenderDiscordId, row.AvatarUrl, row.SenderRole));
            }
        }

        var messages = dbMessages.Select(m => (object)new
        {
            m.Id,
            Channel = dmChannel,
            m.Sender,
            m.SenderDisplayName,
            m.Content,
            m.SentAt,
            m.AvatarUrl,
            m.SenderRole
        });

        await Clients.Caller.SendAsync("LobbyHistory", new
        {
            Channel = dmChannel,
            Messages = messages
        });
    }

    public async Task SendDirectMessage(string targetUsername, string content)
    {
        if (string.IsNullOrWhiteSpace(targetUsername))
            throw new HubException("Target username is required.");

        if (string.IsNullOrWhiteSpace(content))
            throw new HubException("Message content cannot be empty.");

        var senderDiscordId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? Context.User?.FindFirstValue("nameidentifier")
            ?? Context.User?.FindFirstValue("sub")
            ?? string.Empty;
        var senderUsername = Context.User?.Identity?.Name
            ?? Context.User?.FindFirstValue("unique_name")
            ?? "Unknown";

        var senderProfile = await _profiles.GetByDiscordIdAsync(senderDiscordId, CancellationToken.None);
        var targetProfile = await _profiles.GetByNameAsync(targetUsername.Trim(), CancellationToken.None);

        if (targetProfile == null)
            throw new HubException($"User '{targetUsername}' was not found.");

        if (targetProfile.DiscordId == senderDiscordId)
            throw new HubException("You cannot send a direct message to yourself.");

        var senderDisplayName = senderProfile?.DiscordDisplayName ?? senderUsername;
        var senderAvatarUrl = senderProfile?.DiscordAvatarUrl;
        var senderRole = senderProfile?.DiscordRank;
        var targetDisplayName = targetProfile.DiscordDisplayName ?? targetProfile.Username;
        var timestamp = DateTime.UtcNow;
        var messageId = Guid.NewGuid().ToString();
        var trimmedContent = content.Trim();
        
        // SHARED CHANNEL for both users
        var dmChannel = BuildPrivateChannelName(senderUsername, targetProfile.Username);

        var message = new ChatMessage(
            Id: messageId,
            Sender: senderUsername,
            SenderDisplayName: senderDisplayName,
            Content: trimmedContent,
            SentAt: timestamp,
            SenderDiscordId: senderDiscordId,
            AvatarUrl: senderAvatarUrl,
            SenderRole: senderRole);

        StoreMessageHistory(dmChannel, message);

        // Persist DM to DB
        _db.LobbyChatMessages.Add(new LobbyChatMessage
        {
            Id = message.Id,
            Channel = dmChannel,
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
            Channel = dmChannel,
            Sender = senderUsername,
            SenderDisplayName = senderDisplayName,
            Content = trimmedContent,
            SentAt = timestamp,
            AvatarUrl = senderAvatarUrl,
            SenderRole = senderRole,
            CounterpartUsername = senderUsername,
            CounterpartDisplayName = senderDisplayName
        });

        // Echo back to the sender
        await Clients.Caller.SendAsync("ReceiveDirectMessage", new
        {
            Id = messageId,
            Channel = dmChannel,
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

    #region Moderation Authorization

    /// <summary>
    /// Verifies the actor may edit or delete a lobby message owned by <paramref name="ownerDiscordId"/>.
    /// Throws <see cref="HubException"/> if the action is denied.
    /// Rules: own content is always allowed; moderators may act on content from
    /// equal-or-lower-ranked members only.
    /// </summary>
    private async Task AuthorizeModerationByDiscordIdAsync(
        string actorDiscordId, string ownerDiscordId, string action)
    {
        if (actorDiscordId == ownerDiscordId)
            return;

        var actor = await _profiles.GetByDiscordIdAsync(actorDiscordId, CancellationToken.None);
        if (!SecurityConstants.IsElevatedRequestRole(actor?.DiscordRank))
            throw new HubException($"You do not have permission to {action} this message.");

        var owner = await _profiles.GetByDiscordIdAsync(ownerDiscordId, CancellationToken.None);
        if (SecurityConstants.GetRolePosition(owner?.DiscordRank) > SecurityConstants.GetRolePosition(actor?.DiscordRank))
            throw new HubException("You cannot moderate content from higher-ranked members.");
    }

    /// <summary>
    /// Verifies the actor may edit or delete a request-room comment owned by <paramref name="ownerProfileId"/>.
    /// </summary>
    private async Task AuthorizeModerationByProfileIdAsync(
        string actorDiscordId, Guid ownerProfileId, string action)
    {
        var actor = await _profiles.GetByDiscordIdAsync(actorDiscordId, CancellationToken.None);
        if (actor?.Id == ownerProfileId)
            return;

        if (!SecurityConstants.IsElevatedRequestRole(actor?.DiscordRank))
            throw new HubException($"You do not have permission to {action} this message.");

        var owner = await _profiles.GetByIdAsync(ownerProfileId, CancellationToken.None);
        if (SecurityConstants.GetRolePosition(owner?.DiscordRank) > SecurityConstants.GetRolePosition(actor?.DiscordRank))
            throw new HubException("You cannot moderate content from higher-ranked members.");
    }

    #endregion

    #region Private Helpers

    /// <summary>
    /// Finds DM messages sent to the current user since they were last seen and notifies them.
    /// </summary>
    private async Task NotifyPendingDirectMessagesAsync(string username, DateTime since)
    {
        try
        {
            var usernameNorm = username.Trim().ToLowerInvariant();

            // Load DMs sent since the user was last online
            var recentDms = await _db.LobbyChatMessages
                .Where(m => m.Channel.StartsWith("dm:") && !m.IsDeleted && m.SentAt > since)
                .ToListAsync();

            var pending = recentDms
                .Where(m =>
                {
                    var parts = m.Channel.Split(':');
                    return parts.Length == 3
                        && (parts[1] == usernameNorm || parts[2] == usernameNorm)
                        && !string.Equals(m.Sender.Trim(), usernameNorm, StringComparison.OrdinalIgnoreCase);
                })
                .GroupBy(m => m.Channel)
                .Select(g =>
                {
                    var latest = g.OrderByDescending(m => m.SentAt).First();
                    return new
                    {
                        Channel = g.Key,
                        UnreadCount = g.Count(),
                        LastSenderUsername = latest.Sender,
                        LastSenderDisplayName = latest.SenderDisplayName
                    };
                })
                .ToList();

            if (pending.Count > 0)
                await Clients.Caller.SendAsync("PendingDirectMessages", pending);
        }
        catch
        {
            // Non-fatal — unread DM check must not interrupt connection flow
        }
    }

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

    private static string BuildPrivateChannelName(string userA, string userB)
    {
        var list = new List<string> { userA.Trim().ToLowerInvariant(), userB.Trim().ToLowerInvariant() };
        list.Sort();
        return $"dm:{list[0]}:{list[1]}";
    }

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

    private static ChatMessage BuildGeneralWelcomeMessage()
    {
        return new ChatMessage(
            Id: "system-welcome-001",
            Sender: "PackTracker",
            SenderDisplayName: "PackTracker",
            Content:
                "# **__PACKTRACKER // HOUSE WOLF OPERATIONS HUB__**\n\n" +
                "> ***Welcome to the command floor.***\n" +
                "> Real-time requests, division comms, blueprint intelligence, logistics flow, and org coordination all run through this channel.\n\n" +
                "---\n\n" +
                "## **__Core Systems__**\n" +
                "- **Active Requests Dashboard**\n" +
                "  Track live **Assistance**, **Crafting**, and **Procurement** work across the org.\n" +
                "- **Crafting Center**\n" +
                "  Submit jobs, coordinate with crafters, and move requests from open to complete.\n" +
                "- **Procurement Queue**\n" +
                "  Source materials, assign logistics, and track delivery progress in real time.\n" +
                "- **Assistance Requests**\n" +
                "  Need backup, transport, security, or rapid support in the verse? Post it and coordinate immediately.\n\n" +
                "## **__Operations Toolkit__**\n" +
                "- **Blueprint Explorer**\n" +
                "  Search the House Wolf blueprint library, inspect materials, and verify requirements before posting work.\n" +
                "- **Trading Hub (UEX)**\n" +
                "  Review commodity pricing, route opportunities, and cargo economics before you launch.\n" +
                "- **Guide Scheduling**\n" +
                "  Leadership can organize tours, training runs, and operational events directly from the dashboard.\n\n" +
                "## **__Comms & Presence__**\n" +
                "- **General + Division Channels**\n" +
                "  Division rooms unlock automatically from your Discord role mapping.\n" +
                "- **Presence + Direct Messages**\n" +
                "  See who is online, open DMs instantly, and keep coordination inside one operational surface.\n\n" +
                "---\n\n" +
                "**__Startup Checklist__**\n" +
                "1. Review the dashboard.\n" +
                "2. Check active requests.\n" +
                "3. Join the right division channel.\n" +
                "4. Claim, coordinate, and execute.\n\n" +
                "||House Wolf runs best when updates are fast, clear, and visible to the pack.||\n\n" +
                "***Log in with Discord and move.***",
            SentAt: new DateTime(2026, 4, 19, 0, 0, 0, DateTimeKind.Utc),
            SenderDiscordId: "system",
            AvatarUrl: null,
            SenderRole: "System");
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


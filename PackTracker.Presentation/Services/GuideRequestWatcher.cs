using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PackTracker.Application.Options;

namespace PackTracker.Presentation.Services;

public class GuideRequestWatcher
{
    private readonly DiscordSocketClient _client;
    private readonly GuideRequestOptions _options;
    private readonly GuideNotificationService _notifier;
    private readonly ILogger<GuideRequestWatcher> _logger;

    public GuideRequestWatcher(
        DiscordSocketClient client,
        IOptions<GuideRequestOptions> options,
        GuideNotificationService notifier,
        ILogger<GuideRequestWatcher> logger)
    {
        _client = client;
        _options = options.Value;
        _notifier = notifier;
        _logger = logger;

        _client.ThreadCreated += OnThreadCreatedAsync;
    }

    private async Task OnThreadCreatedAsync(SocketThreadChannel thread)
    {
        try
        {
            if (thread.ParentChannel.Id != _options.ForumChannelId)
                return;

            if (thread.ParentChannel is not SocketForumChannel forum)
                return;

            var scheduledTag = forum.Tags.FirstOrDefault(t =>
                string.Equals(t.Name, "scheduled", StringComparison.OrdinalIgnoreCase));

            if (scheduledTag == null || !thread.AppliedTags.Contains(scheduledTag.Id))
                return;

            _logger.LogInformation("Detected new scheduled guide request: {Thread}", thread.Name);

            await Task.Delay(1500); // brief wait for author population
            var requester = thread.Owner ?? await thread.GetOwnerAsync();
            if (requester == null) return;

            var embed = new EmbedBuilder()
                .WithTitle("📋 Guide Request Logged")
                .WithDescription(
                    $"Hey {requester.Mention}, your guide request has been received!\n" +
                    "Our team will review it and assign a guide shortly.\n\n" +
                    "Once assigned, this thread will show your guide’s name and status.")
                .WithColor(Color.DarkTeal)
                .WithFooter("House Wolf Guide Coordination System")
                .Build();

            await thread.SendMessageAsync(embed: embed);

            try
            {
                await requester.SendMessageAsync(
                    $"Hello {requester.Username}! 🐺\n" +
                    $"We received your guide request: **{thread.Name}**.\n" +
                    "Our team will assign a guide shortly right here in Discord.");
            }
            catch { /* user might have DMs off */ }

            await _notifier.NotifyGuidesAsync(thread, requester);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling guide request thread {Thread}", thread.Id);
        }
    }
}

public static partial class SocketThreadChannelExtensions
{
    public static async Task<IUser?> GetOwnerAsync(this SocketThreadChannel thread)
    {
        var message = await thread.GetMessageAsync(thread.Id);
        return message?.Author;
    }
}

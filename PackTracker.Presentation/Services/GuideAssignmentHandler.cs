using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace PackTracker.Presentation.Services;

public class GuideAssignmentHandler
{
    private readonly DiscordSocketClient _client;
    private readonly ILogger<GuideAssignmentHandler> _logger;

    public GuideAssignmentHandler(DiscordSocketClient client, ILogger<GuideAssignmentHandler> logger)
    {
        _client = client;
        _logger = logger;
        _client.ButtonExecuted += OnButtonExecutedAsync;
    }

    private async Task OnButtonExecutedAsync(SocketMessageComponent component)
    {
        try
        {
            if (!component.Data.CustomId.StartsWith("claim_") &&
                !component.Data.CustomId.StartsWith("complete_") &&
                !component.Data.CustomId.StartsWith("cancel_"))
                return;

            await component.DeferAsync(ephemeral: true);

            var (action, threadIdStr) = (component.Data.CustomId.Split('_')[0], component.Data.CustomId.Split('_')[1]);
            if (!ulong.TryParse(threadIdStr, out var threadId))
                return;

            var thread = _client.GetChannel(threadId) as SocketThreadChannel;
            if (thread == null)
            {
                await component.FollowupAsync("❌ Could not locate the thread.", ephemeral: true);
                return;
            }

            var forum = thread.ParentChannel as SocketForumChannel;
            if (forum == null) return;

            var user = component.User;
            _logger.LogInformation("Guide action {Action} on thread {Thread} by {User}", action, thread.Name, user);

            switch (action)
            {
                case "claim":
                    await thread.SendMessageAsync($"✅ {user.Mention} has **claimed** this guide request.");
                    await SetTagAsync(thread, forum, "assigned");
                    await component.FollowupAsync($"You claimed **{thread.Name}**.", ephemeral: true);
                    break;

                case "complete":
                    await thread.SendMessageAsync($"🏁 {user.Mention} marked this request as **Completed**. Great job!");
                    await SetTagAsync(thread, forum, "completed");
                    await component.FollowupAsync($"Thread marked completed.", ephemeral: true);
                    break;

                case "cancel":
                    await thread.SendMessageAsync($"❌ Request cancelled by {user.Mention}. Thread closed.");
                    await SetTagAsync(thread, forum, "cancelled");
                    await component.FollowupAsync($"Thread cancelled.", ephemeral: true);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing guide button.");
        }
    }

    private static async Task SetTagAsync(SocketThreadChannel thread, SocketForumChannel forum, string tagName)
    {
        var tag = forum.Tags.FirstOrDefault(t => string.Equals(t.Name, tagName, StringComparison.OrdinalIgnoreCase));
        if (tag != null)
            await thread.ModifyAsync(p => p.AppliedTags = new List<ulong> { tag.Id });
    }
}

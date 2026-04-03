using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Options;
using PackTracker.Application.Options;

namespace PackTracker.Presentation.Services;

public class GuideNotificationService
{
    private readonly DiscordSocketClient _client;
    private readonly GuideRequestOptions _options;

    public GuideNotificationService(DiscordSocketClient client, IOptions<GuideRequestOptions> options)
    {
        _client = client;
        _options = options.Value;
    }

    public async Task NotifyGuidesAsync(SocketThreadChannel thread, IUser requester)
    {
        if (_client.GetChannel(_options.StaffNotifyChannelId) is not IMessageChannel channel)
            return;

        var roleMention = $"<@&{_options.GuideRoleId}>";

        var embed = new EmbedBuilder()
            .WithTitle("🗓️ New Scheduled Guide Request")
            .WithDescription(
                $"**Requester:** {requester.Mention}\n" +
                $"**Thread:** [{thread.Name}]({thread.GetJumpUrl()})\n\n" +
                "Guides — react with 🐺 or click **Claim Guide** to take this request.")
            .WithColor(Color.Orange)
            .WithCurrentTimestamp()
            .Build();

        var components = new ComponentBuilder()
            .WithButton("Claim Guide", $"claim_{thread.Id}", ButtonStyle.Primary, new Emoji("🐺"))
            .WithButton("Mark Complete", $"complete_{thread.Id}", ButtonStyle.Success, new Emoji("✅"))
            .WithButton("Cancel Request", $"cancel_{thread.Id}", ButtonStyle.Danger, new Emoji("❌"))
            .Build();

        await channel.SendMessageAsync($"{roleMention}", embed: embed, components: components);
    }
}

public static partial class SocketThreadChannelExtensions
{
    public static string GetJumpUrl(this SocketThreadChannel thread)
    {
        return $"https://discord.com/channels/{thread.Guild.Id}/{thread.Id}";
    }
}
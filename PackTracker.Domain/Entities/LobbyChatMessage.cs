namespace PackTracker.Domain.Entities;

public class LobbyChatMessage
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Channel { get; set; } = string.Empty;
    public string Sender { get; set; } = string.Empty;
    public string SenderDisplayName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public string SenderDiscordId { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string? SenderRole { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? EditedAt { get; set; }
}

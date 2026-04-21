namespace PackTracker.Domain.Entities;

/// <summary>
/// Temporary storage for desktop client OAuth completion.
/// This replaces IMemoryCache for polling, allowing for horizontal scalability.
/// </summary>
public sealed class LoginState
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// The unique state string passed during the OAuth flow.
    /// </summary>
    public string ClientState { get; set; } = string.Empty;

    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public int ExpiresIn { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddMinutes(5);

    /// <summary>
    /// After 5 minutes, this state is considered expired.
    /// </summary>
    public bool IsExpired => DateTime.UtcNow > ExpiresAt;
}

namespace PackTracker.Application.Interfaces;

public interface IAuthWorkflowService
{
    Task<AuthCompletionResult> CompleteAsync(AuthCompletionRequest request, CancellationToken cancellationToken);

    Task<LoginTokenPayload?> PollAsync(string clientState, CancellationToken cancellationToken);

    Task<AuthRefreshResult> RefreshAsync(string refreshToken, CancellationToken cancellationToken);

    Task LogoutAsync(string refreshToken, CancellationToken cancellationToken);
}

public sealed record AuthCompletionRequest(
    string DiscordId,
    string Username,
    string? DisplayName,
    string? AvatarUrl,
    string Discriminator,
    string AccessToken,
    string? ClientState);

public sealed record AuthCompletionResult(
    AuthCompletionStatus Status,
    string Message);

public enum AuthCompletionStatus
{
    Success,
    AccessDenied
}

public sealed record AuthRefreshResult(
    bool Succeeded,
    string? AccessToken,
    string? RefreshToken,
    int? ExpiresIn,
    string Message);

public sealed record LoginTokenPayload(string AccessToken, string RefreshToken, int ExpiresIn);

namespace PackTracker.Application.Interfaces;

public interface ICurrentUserService
{
    string UserId { get; }
    string DisplayName { get; }
    string? Username => null;
    bool IsAuthenticated { get; }
    IReadOnlyCollection<string> Roles => Array.Empty<string>();
    bool IsInRole(string role);
}

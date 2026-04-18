namespace PackTracker.Application.Interfaces;

public interface ICurrentUserService
{
    string UserId { get; }
    string DisplayName { get; }
    bool IsAuthenticated { get; }
    bool IsInRole(string role);
}

using PackTracker.Domain.Entities;

namespace PackTracker.Application.Interfaces;

public interface ICurrentUserProfileResolver
{
    Task<CurrentUserProfileContext> ResolveAsync(CancellationToken cancellationToken);
}

public sealed record CurrentUserProfileContext(string DiscordId, Profile? Profile)
{
    public Guid? ProfileId => Profile?.Id;
}

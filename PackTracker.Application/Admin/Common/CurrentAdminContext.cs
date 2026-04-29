using PackTracker.Domain.Security;

namespace PackTracker.Application.Admin.Common;

public sealed record CurrentAdminContext(
    Guid ProfileId,
    string DisplayName,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> Permissions,
    AdminTier? HighestTier,
    bool CanAccessAdmin);

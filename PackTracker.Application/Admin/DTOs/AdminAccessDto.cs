namespace PackTracker.Application.Admin.DTOs;

public sealed record AdminAccessDto(
    bool CanAccessAdmin,
    string? HighestTier,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> Permissions)
{
    public AdminAccessDto()
        : this(false, "None", Array.Empty<string>(), Array.Empty<string>())
    {
    }
}
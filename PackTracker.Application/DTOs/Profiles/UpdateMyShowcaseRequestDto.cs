namespace PackTracker.Application.DTOs.Profiles;

public sealed record UpdateMyShowcaseRequestDto(
    string? ShowcaseImageUrl,
    string? ShowcaseEyebrow,
    string? ShowcaseTagline,
    string? ShowcaseBio);

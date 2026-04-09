namespace PackTracker.Application.DTOs.Crafting;

public sealed class BlueprintOwnersResponseDto
{
    public Guid Id { get; set; }          // local DB blueprint id
    public Guid WikiUuid { get; set; }    // wiki blueprint id
    public int OwnerCount { get; set; }
    public IReadOnlyList<BlueprintOwnerDto> Owners { get; set; } = Array.Empty<BlueprintOwnerDto>();
}
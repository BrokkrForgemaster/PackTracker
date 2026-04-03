using PackTracker.Application.DTOs.Regolith;

namespace PackTracker.Application.Interfaces;

public interface IRegolithService
{
    Task<RegolithProfileDto?> GetProfileAsync(CancellationToken ct = default);

    Task<IReadOnlyList<RegolithRefineryJobDto>> GetRefineryJobsAsync(CancellationToken ct = default);
}

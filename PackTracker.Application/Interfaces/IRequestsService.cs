using PackTracker.Application.DTOs.Request;
using PackTracker.Domain.Enums;

namespace PackTracker.Application.Interfaces;

public interface IRequestsService
{
    Task<List<RequestDto>> QueryAsync(RequestStatus? status = null, RequestKind? kind = null, bool? mine = null, int top = 100, CancellationToken ct = default);
    Task<RequestDto?> CreateAsync(RequestCreateDto dto, CancellationToken ct = default);
    Task<RequestDto?> UpdateAsync(int id, RequestUpdateDto dto, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
    Task<RequestDto?> CompleteAsync(int id, CancellationToken ct = default);
}

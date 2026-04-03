using System.Collections;
using PackTracker.Application.DTOs.KillTracker;
using PackTracker.Domain.Entities;

namespace PackTracker.Application.Interfaces
{
    /// <summary name="IKillEventService">
    /// Contract for kill event management, real-time processing, and sync.
    /// </summary>
    public interface IKillEventService : IDisposable
    {
        event Action<KillEntity>? KillReceived;
        KillEntity? LastKill { get; }
        void Raise(KillEntity entry);
        Task RaiseAsync(KillEntity entry);
        Task<IEnumerable<KillEntity>> GetRecentKillsAsync(string userId, int count);
        Task<KillStatsDto> GetKillStatsAsync(string userId);
        Task<List<KillDto>> GetKillsByTypeAsync(string userId, string killType, int count);
        Task SyncKillsFromGameLogAsync(string userId);
        Task<List<LeaderDto>> GetTopKillersByTypeAsync(string fps, int p1);
    }
}
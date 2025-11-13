using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using PackTracker.Application.DTOs.KillTracker;
using PackTracker.Application.DTOs.Request;
using PackTracker.Application.Interfaces;
using PackTracker.Presentation.Services;

namespace PackTracker.Presentation.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly IKillEventService _killEventService;
    private readonly IApiClientProvider _apiClient;
    private readonly IRegolithService _regolithService;

    private readonly ObservableCollection<LeaderCardModel> _fpsLeaders = new();
    private readonly ObservableCollection<LeaderCardModel> _airLeaders = new();

    public ReadOnlyObservableCollection<LeaderCardModel> Fps { get; }
    public ReadOnlyObservableCollection<LeaderCardModel> AirLeaders { get; }
    public GuideDashboardViewModel Guide { get; }
    public ObservableCollection<RequestDto> ActiveRequests { get; } = new();
    [ObservableProperty]
    private MiningSummaryModel _mining = MiningSummaryModel.Empty;

    public int TotalKills { get; set; }
    public double AverageRoi { get; set; }
    public int ActiveMember { get; set; }
    public int CompletedOps { get; set; }

    public DashboardViewModel(
        IKillEventService killEventService,
        GuideDashboardViewModel guide,
        IRegolithService regolithService,
        IApiClientProvider apiClient)
    {
        _killEventService = killEventService ?? throw new ArgumentNullException(nameof(killEventService));
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        _regolithService = regolithService ?? throw new ArgumentNullException(nameof(regolithService));
        Guide = guide;

        Fps = new ReadOnlyObservableCollection<LeaderCardModel>(_fpsLeaders);
        AirLeaders = new ReadOnlyObservableCollection<LeaderCardModel>(_airLeaders);

        _ = LoadLeadersAsync();
        _ = Guide.RefreshAsync();
        _ = LoadActiveRequestsAsync();
        _ = LoadMiningSummaryAsync();
    }

    private async Task LoadLeadersAsync()
    {
        var fpsLeaders = await _killEventService.GetTopKillersByTypeAsync("FpsLeaders", 5);
        var airLeaders = await _killEventService.GetTopKillersByTypeAsync("AirLeaders", 5);

        _fpsLeaders.Clear();
        foreach (var leader in fpsLeaders)
        {
            _fpsLeaders.Add(new LeaderCardModel
            {
                Username = leader.Attacker,
                KillCount = leader.KillCount,
                MostUsedWeapon = leader.MostUsedWeapon
            });
        }

        _airLeaders.Clear();
        foreach (var leader in airLeaders)
        {
            _airLeaders.Add(new LeaderCardModel
            {
                Username = leader.Attacker,
                KillCount = leader.KillCount,
                MostUsedWeapon = leader.MostUsedWeapon
            });
        }
    }

    private async Task LoadActiveRequestsAsync()
    {
        try
        {
            // Fetch top 5 open requests ordered by priority
            using var client = _apiClient.CreateClient();
            var response = await client.GetFromJsonAsync<ApiResponse<List<RequestDto>>>("api/v1/requests?status=0&top=5");

            ActiveRequests.Clear();
            if (response?.Data != null)
            {
                foreach (var request in response.Data.OrderByDescending(r => r.Priority))
                {
                    ActiveRequests.Add(request);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load active requests: {ex.Message}");
        }
    }

    private async Task LoadMiningSummaryAsync()
    {
        try
        {
            var jobs = await _regolithService.GetRefineryJobsAsync();
            if (jobs.Count == 0)
            {
                Mining = MiningSummaryModel.Empty;
                return;
            }

            var active = jobs.Where(j => !string.Equals(j.Status, "complete", StringComparison.OrdinalIgnoreCase) &&
                                         !string.Equals(j.Status, "completed", StringComparison.OrdinalIgnoreCase)).ToList();

            var pendingQuantity = active.Sum(j => j.Quantity);
            var projectedYield = active.Sum(j => j.Yield);
            var nextEta = active
                .Select(j => j.Eta ?? j.CompletedAt ?? (DateTime?)DateTime.MinValue)
                .Where(dt => dt > (DateTime?)DateTime.MinValue)
                .Min();

            Mining = new MiningSummaryModel
            {
                ActiveJobs = active.Count,
                PendingQuantity = pendingQuantity,
                ProjectedYield = projectedYield,
                NextCompletionUtc = nextEta,
                LastSyncedUtc = jobs.Max(j => j.SyncedAt)
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load mining summary: {ex.Message}");
            Mining = MiningSummaryModel.Empty;
        }
    }


    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public int? Count { get; set; }
        public T? Data { get; set; }
    }

    public sealed class MiningSummaryModel
    {
        public static MiningSummaryModel Empty { get; } = new();

        public int ActiveJobs { get; init; }
        public double PendingQuantity { get; init; }
        public double ProjectedYield { get; init; }
        public DateTime? NextCompletionUtc { get; init; }
        public DateTime? LastSyncedUtc { get; init; }

        public string NextCompletionDisplay =>
            NextCompletionUtc is null ? "No ETA" : NextCompletionUtc.Value.ToLocalTime().ToString("MMM d · HH:mm");
    }
}

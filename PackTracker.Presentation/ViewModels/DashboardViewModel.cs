using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using PackTracker.Application.DTOs.KillTracker;
using PackTracker.Application.Interfaces;

namespace PackTracker.Presentation.ViewModels;

public class DashboardViewModel
{
    private readonly IKillEventService _killEventService;

    private ObservableCollection<LeaderCardModel> FpsLeaders { get; } = new();
    private ObservableCollection<LeaderCardModel> AirLeaders { get; } = new();

    public int TotalKills { get; set; }
    public double AverageRoi { get; set; }
    public int ActiveMember { get; set; }
    public int CompletedOps { get; set; }

    public DashboardViewModel(IKillEventService killEventService)
    {
        _killEventService = killEventService ?? throw new ArgumentNullException(nameof(killEventService));
        _ = RefreshLeadersPeriodicallyAsync();
    }

    private async Task LoadLeadersAsync()
    {
        var fpsLeaders = await _killEventService.GetTopKillersByTypeAsync("FpsLeaders", 5);
        var airLeaders = await _killEventService.GetTopKillersByTypeAsync("AirLeaders", 5);

        FpsLeaders.Clear();
        foreach (var leader in fpsLeaders)
        {
            FpsLeaders.Add(new LeaderCardModel
            {
                Username = leader.Attacker,
                KillCount = leader.KillCount,
                MostUsedWeapon = leader.MostUsedWeapon
            });
        }

        AirLeaders.Clear();
        foreach (var leader in airLeaders)
        {
            AirLeaders.Add(new LeaderCardModel
            {
                Username = leader.Attacker,
                KillCount = leader.KillCount,
                MostUsedWeapon = leader.MostUsedWeapon
            });
        }
    }

    private async Task RefreshLeadersPeriodicallyAsync()
    {
        while (true)
        {
            await LoadLeadersAsync();
            await Task.Delay(10000);
        }
    }
}
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using PackTracker.Application.DTOS;
using PackTracker.Application.Interfaces;

namespace PackTracker.Presentation.ViewModels;

public class DashboardViewModel
{
    private readonly IKillEventService _killEventService;
    public ObservableCollection<LeaderCardModel> FpsLeaders { get; } = new();
    public ObservableCollection<LeaderCardModel> AirLeaders { get; } = new();

    public DashboardViewModel(IKillEventService killEventService)
    {
        _killEventService = killEventService;
        _ = LoadLeadersAsync();
    }

    private async Task LoadLeadersAsync()
    {
        var fpsLeaders = await _killEventService.GetTopKillersByTypeAsync("FPS", 5);
        var airLeaders = await _killEventService.GetTopKillersByTypeAsync("Air", 5);

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
}


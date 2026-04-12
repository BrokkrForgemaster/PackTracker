using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Net.Http.Json;
using System.Threading.Tasks;
using PackTracker.Domain.Entities;
using PackTracker.Presentation.Services;

namespace PackTracker.Presentation.ViewModels;

public partial class GuideDashboardViewModel : ObservableObject
{
    private readonly IApiClientProvider _apiClient;

    [ObservableProperty]
    private ObservableCollection<GuideRequest> _requests = new();

    public GuideDashboardViewModel(IApiClientProvider apiClient)
    {
        _apiClient = apiClient;
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        using var client = _apiClient.CreateClient();
        var result = await client.GetFromJsonAsync<List<GuideRequest>>("api/v1/guides/scheduled");
        if (result != null)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                Requests.Clear();
                foreach (var r in result)
                    Requests.Add(r);
            });
        }
    }
}

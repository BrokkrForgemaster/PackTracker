using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using PackTracker.Application.DTOs.Request;
using PackTracker.Presentation.Services;

namespace PackTracker.Presentation.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly IApiClientProvider _apiClient;

    public GuideDashboardViewModel Guide { get; }
    public ObservableCollection<RequestDto> ActiveRequests { get; } = new();

    public DashboardViewModel(
        GuideDashboardViewModel guide,
        IApiClientProvider apiClient)
    {
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        Guide = guide;

        _ = Guide.RefreshAsync();
        _ = LoadActiveRequestsAsync();
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

    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public int? Count { get; set; }
        public T? Data { get; set; }
    }
}

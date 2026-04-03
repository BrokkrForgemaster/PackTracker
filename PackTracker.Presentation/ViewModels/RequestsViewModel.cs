using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using PackTracker.Application.DTOs.Request;
using PackTracker.Domain.Enums;
using PackTracker.Presentation.Services;
using PackTracker.Presentation.Views;

namespace PackTracker.Presentation.ViewModels;

    public partial class RequestsViewModel : ObservableObject, IAsyncDisposable
    {
        private readonly IApiClientProvider _apiClientProvider;
        private readonly HubConnection _hub;
        private readonly IServiceProvider _serviceProvider;
        private readonly string _apiBaseUrl;

        [ObservableProperty] private RequestDto? selectedRequest;
        public ObservableCollection<RequestDto> Requests { get; } = new();

        public List<RequestKind> KindOptions { get; } = Enum.GetValues<RequestKind>().ToList();
        public List<RequestStatus> StatusOptions { get; } = Enum.GetValues<RequestStatus>().ToList();

        [ObservableProperty] private RequestKind? selectedKind;
        [ObservableProperty] private RequestStatus? selectedStatus;

        public RequestsViewModel(IServiceProvider serviceProvider, IApiClientProvider apiClientProvider)
        {
            _serviceProvider = serviceProvider;
            _apiClientProvider = apiClientProvider;
            _apiBaseUrl = apiClientProvider.BaseUrl;
            _hub = new HubConnectionBuilder()
                .WithUrl($"{_apiBaseUrl}/hubs/requests")
                .WithAutomaticReconnect()
                .Build();


            _hub.On<RequestDto>("RequestUpdated", OnRequestUpdated);
            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            try
            {
                await _hub.StartAsync().ConfigureAwait(false);
                await RefreshAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SignalR connection failed: {ex.Message}");
            }
        }

        private void OnRequestUpdated(RequestDto dto)
        {
            var existing = Requests.FirstOrDefault(r => r.Id == dto.Id);
            if (existing == null) Requests.Insert(0, dto);
            else Requests[Requests.IndexOf(existing)] = dto;
        }

        [RelayCommand]
        private async Task RefreshAsync()
        {
            var query = new List<string>();
            if (SelectedKind is not null) query.Add($"kind={(int)SelectedKind}");
            if (SelectedStatus is not null) query.Add($"status={(int)SelectedStatus}");
            var url = "api/v1/requests";
            if (query.Count > 0) url += "?" + string.Join("&", query);

            try
            {
                using var client = _apiClientProvider.CreateClient();
                var result = await client.GetFromJsonAsync<ApiResponse<List<RequestDto>>>(url);
                Requests.Clear();
                foreach (var r in result?.Data ?? [])
                    Requests.Add(r);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to refresh requests: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task NewRequestAsync()
        {
            var viewModel = _serviceProvider.GetRequiredService<NewRequestViewModel>();
            var dialog = new NewRequestDialog(viewModel);
            dialog.Owner = System.Windows.Application.Current.MainWindow;
            var result = dialog.ShowDialog();

            // Refresh if the request was successfully submitted
            if (result == true)
            {
                await RefreshAsync();
            }
        }


        [RelayCommand]
        private async Task CompleteAsync()
        {
            if (SelectedRequest == null) return;
            using var client = _apiClientProvider.CreateClient();
            await client.PatchAsync($"api/v1/requests/{SelectedRequest.Id}/complete", null);
            await RefreshAsync();
        }

        [RelayCommand]
        private async Task DeleteAsync()
        {
            if (SelectedRequest == null) return;
            using var client = _apiClientProvider.CreateClient();
            await client.DeleteAsync($"api/v1/requests/{SelectedRequest.Id}");
            await RefreshAsync();
        }

        [RelayCommand]
        private async Task EditAsync()
        {
            if (SelectedRequest == null) return;
            // TODO: Implement edit dialog
        }

        [RelayCommand]
        private async Task ClaimAsync()
        {
            if (SelectedRequest == null) return;
            try
            {
                using var client = _apiClientProvider.CreateClient();
                await client.PatchAsync($"api/v1/requests/{SelectedRequest.Id}/claim", null);
                await RefreshAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to claim request: {ex.Message}");
            }
        }

        [RelayCommand]
        private void SelectRequest(RequestDto request)
        {
            SelectedRequest = request;
        }

        public async ValueTask DisposeAsync()
        {
            if (_hub != null)
                await _hub.DisposeAsync();
        }
    }

    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public int? Count { get; set; }
        public T? Data { get; set; }
    }


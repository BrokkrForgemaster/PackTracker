using System.Net.Http;
using System.Net.Http.Json;
using PackTracker.Domain.Enums;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using Microsoft.AspNetCore.SignalR.Client;
using CommunityToolkit.Mvvm.ComponentModel;
using PackTracker.Application.DTOs.Request;
using PackTracker.Presentation.Views;

namespace PackTracker.Presentation.ViewModels;

    public partial class RequestsViewModel : ObservableObject, IAsyncDisposable
    {
        private readonly HttpClient _http;
        private readonly HubConnection _hub;

        [ObservableProperty] private RequestDto? selectedRequest;
        public ObservableCollection<RequestDto> Requests { get; } = new();

        public List<RequestKind> KindOptions { get; } = Enum.GetValues<RequestKind>().ToList();
        public List<RequestStatus> StatusOptions { get; } = Enum.GetValues<RequestStatus>().ToList();

        [ObservableProperty] private RequestKind? selectedKind;
        [ObservableProperty] private RequestStatus? selectedStatus;

        public RequestsViewModel()
        {
            _http = new HttpClient { BaseAddress = new Uri("http://localhost:5001/") };
            var baseUrl = _http.BaseAddress?.ToString() ?? throw new InvalidOperationException("HttpClient BaseAddress is not set.");
            _hub = new HubConnectionBuilder()
                .WithUrl($"{baseUrl}hubs/requests")
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
                var result = await _http.GetFromJsonAsync<ApiResponse<List<RequestDto>>>(url);
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
        private void NewRequest()
        {
            var dialog = new NewRequestDialog();
            dialog.Owner = System.Windows.Application.Current.MainWindow;
            dialog.ShowDialog();
        }


        [RelayCommand]
        private async Task CompleteAsync()
        {
            if (SelectedRequest == null) return;
            await _http.PatchAsync($"api/v1/requests/{SelectedRequest.Id}/complete", null);
            await RefreshAsync();
        }

        [RelayCommand]
        private async Task DeleteAsync()
        {
            if (SelectedRequest == null) return;
            await _http.DeleteAsync($"api/v1/requests/{SelectedRequest.Id}");
            await RefreshAsync();
        }

        [RelayCommand]
        private async Task EditAsync()
        {
            if (SelectedRequest == null) return;
            // TODO: Implement edit dialog
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


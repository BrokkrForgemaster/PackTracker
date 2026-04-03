using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PackTracker.Application.DTOs.Request;
using PackTracker.Domain.Enums;
using PackTracker.Presentation.Services;

namespace PackTracker.Presentation.ViewModels;

public partial class NewRequestViewModel : ObservableObject
{
    private readonly IApiClientProvider _apiClient;

    public NewRequestViewModel(IApiClientProvider apiClient)
    {
        _apiClient = apiClient;

        // Set default values
        SelectedKind = RequestKind.MiningMaterials;
        SelectedPriority = RequestPriority.Normal;
    }

    // Request Kind Options
    public List<RequestKind> KindOptions => new()
    {
        RequestKind.MiningMaterials,
        RequestKind.TradingGoods,
        RequestKind.ShipComponents,
        RequestKind.MissionBackup,
        RequestKind.CargoEscort,
        RequestKind.CombatSupport,
        RequestKind.ShipCrew,
        RequestKind.Transportation,
        RequestKind.LocationScout,
        RequestKind.Guidance,
        RequestKind.EventSupport,
        RequestKind.Other
    };

    // Priority Options
    public List<RequestPriority> PriorityOptions => new()
    {
        RequestPriority.Low,
        RequestPriority.Normal,
        RequestPriority.High,
        RequestPriority.Critical
    };

    // Basic Fields
    [ObservableProperty] private string title = "";
    [ObservableProperty] private string description = "";
    [ObservableProperty] private RequestKind selectedKind;
    [ObservableProperty] private RequestPriority selectedPriority;

    // Material/Resource Fields
    [ObservableProperty] private string? materialName;
    [ObservableProperty] private int? quantityNeeded;
    [ObservableProperty] private string? meetingLocation;
    [ObservableProperty] private string? rewardOffered;
    [ObservableProperty] private int? numberOfHelpersNeeded;

    public NewRequestViewModel()
    {
        throw new NotImplementedException();
    }

    // Conditional visibility properties
    partial void OnSelectedKindChanged(RequestKind value)
    {
        // Update UI based on request kind
        OnPropertyChanged(nameof(ShowMaterialFields));
        OnPropertyChanged(nameof(ShowCrewFields));
    }

    public bool ShowMaterialFields => SelectedKind is RequestKind.MiningMaterials
        or RequestKind.TradingGoods
        or RequestKind.ShipComponents;

    public bool ShowCrewFields => SelectedKind is RequestKind.ShipCrew
        or RequestKind.CargoEscort
        or RequestKind.CombatSupport
        or RequestKind.MissionBackup;

    [RelayCommand]
    private async Task SubmitAsync()
    {
        try
        {
            // Validation
            if (string.IsNullOrWhiteSpace(Title))
            {
                MessageBox.Show("Please enter a title for your request.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(Description))
            {
                MessageBox.Show("Please enter a description for your request.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dto = new RequestCreateDto
            {
                Title = Title,
                Description = Description,
                Kind = SelectedKind,
                Priority = SelectedPriority,
                MaterialName = MaterialName,
                QuantityNeeded = QuantityNeeded,
                MeetingLocation = MeetingLocation,
                RewardOffered = RewardOffered,
                NumberOfHelpersNeeded = NumberOfHelpersNeeded,
                // Keep old fields empty for backwards compatibility
                SkillObjective = "",
                GameBuild = "",
                PlayerHandle = "",
                TimeZone = "",
                HasMic = false,
                PlatformSpecs = "",
                Availability = "",
                CurrentBaseline = "",
                AssetsShips = "",
                Urgency = "",
                GroupPreference = "",
                SuccessCriteria = "",
                RecordingPermission = ""
            };

            using var client = _apiClient.CreateClient();
            var response = await client.PostAsJsonAsync("api/v1/requests", dto);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException(error);
            }

            MessageBox.Show("✅ Request submitted successfully!\n\nYour request is now visible to all org members.", "Success",
                MessageBoxButton.OK, MessageBoxImage.Information);

            // Signal success to close dialog
            RequestSubmitted?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to submit request:\n\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public event EventHandler? RequestSubmitted;
}

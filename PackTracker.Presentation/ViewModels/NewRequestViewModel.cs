using System.Collections.Generic;
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

/// <summary>
/// ViewModel for creating a new request.
/// </summary>
public partial class NewRequestViewModel : ObservableObject
{
    #region Fields

    private readonly IApiClientProvider _apiClient;

    #endregion

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="NewRequestViewModel"/> class.
    /// </summary>
    public NewRequestViewModel(IApiClientProvider apiClient)
    {
        _apiClient = apiClient;

        // Default values
        SelectedKind = RequestKind.MiningMaterials;
        SelectedPriority = RequestPriority.Normal;
    }

    #endregion

    #region Options

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

    public List<RequestPriority> PriorityOptions => new()
    {
        RequestPriority.Low,
        RequestPriority.Normal,
        RequestPriority.High,
        RequestPriority.Critical
    };

    #endregion

    #region Properties

    [ObservableProperty] private string title = "";
    [ObservableProperty] private string description = "";
    [ObservableProperty] private RequestKind selectedKind;
    [ObservableProperty] private RequestPriority selectedPriority;

    [ObservableProperty] private string? materialName;
    [ObservableProperty] private int? quantityNeeded;
    [ObservableProperty] private string? meetingLocation;
    [ObservableProperty] private string? rewardOffered;
    [ObservableProperty] private int? numberOfHelpersNeeded;

    #endregion

    #region UI Logic

    partial void OnSelectedKindChanged(RequestKind value)
    {
        OnPropertyChanged(nameof(ShowMaterialFields));
        OnPropertyChanged(nameof(ShowCrewFields));
    }

    public bool ShowMaterialFields => SelectedKind is
        RequestKind.MiningMaterials or
        RequestKind.TradingGoods or
        RequestKind.ShipComponents;

    public bool ShowCrewFields => SelectedKind is
        RequestKind.ShipCrew or
        RequestKind.CargoEscort or
        RequestKind.CombatSupport or
        RequestKind.MissionBackup;

    #endregion

    #region Commands

    [RelayCommand]
    private async Task SubmitAsync()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(Title))
            {
                MessageBox.Show("Please enter a title.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(Description))
            {
                MessageBox.Show("Please enter a description.", "Validation Error",
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
            };

            using var client = _apiClient.CreateClient();
            var response = await client.PostAsJsonAsync("api/v1/requests", dto);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException(error);
            }

            MessageBox.Show(
                "✅ Request submitted successfully!",
                "Success",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            RequestSubmitted?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Failed to submit request:\n\n{ex.Message}",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    #endregion

    #region Events

    public event EventHandler? RequestSubmitted;

    #endregion
}
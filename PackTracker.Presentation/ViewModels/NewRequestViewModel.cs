using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
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
    [ObservableProperty] private string? quantityNeededText;
    [ObservableProperty] private string? meetingLocation;
    [ObservableProperty] private string? rewardOffered;
    [ObservableProperty] private string? numberOfHelpersNeededText;

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

            if (!TryParseOptionalPositiveInteger(QuantityNeededText, out var quantityNeeded))
            {
                MessageBox.Show(
                    "Quantity must be a whole number greater than zero.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (!TryParseOptionalPositiveInteger(NumberOfHelpersNeededText, out var numberOfHelpersNeeded))
            {
                MessageBox.Show(
                    "Helpers needed must contain at least one whole number. For flexible staffing, enter something like '3-4' or '3 or 4'.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var dto = new RequestCreateDto
            {
                Title = Title,
                Description = Description,
                Kind = SelectedKind,
                Priority = SelectedPriority,
                MaterialName = MaterialName,
                QuantityNeeded = quantityNeeded,
                MeetingLocation = MeetingLocation,
                RewardOffered = RewardOffered,
                NumberOfHelpersNeeded = numberOfHelpersNeeded,
            };

            using var client = _apiClient.CreateClient();
            var response = await client.PostAsJsonAsync("api/v1/requests", dto);

            if (!response.IsSuccessStatusCode)
            {
                var error = await BuildErrorMessageAsync(response);
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

    #region Helpers

    private static bool TryParseOptionalPositiveInteger(string? text, out int? value)
    {
        value = null;

        if (string.IsNullOrWhiteSpace(text))
            return true;

        var matches = Regex.Matches(text, @"\d+")
            .Select(match => int.TryParse(match.Value, out var parsed) ? parsed : 0)
            .Where(parsed => parsed > 0)
            .ToList();

        if (matches.Count == 0)
            return false;

        value = matches.Max();
        return true;
    }

    private static async Task<string> BuildErrorMessageAsync(HttpResponseMessage response)
    {
        var raw = (await response.Content.ReadAsStringAsync()).Trim();
        if (string.IsNullOrWhiteSpace(raw))
            return $"Request failed with status {(int)response.StatusCode}.";

        try
        {
            using var document = JsonDocument.Parse(raw);
            var root = document.RootElement;

            if (root.ValueKind == JsonValueKind.Object)
            {
                if (root.TryGetProperty("error", out var errorElement)
                    && errorElement.ValueKind == JsonValueKind.String)
                {
                    return errorElement.GetString() ?? raw;
                }

                if (root.TryGetProperty("title", out var titleElement)
                    && titleElement.ValueKind == JsonValueKind.String)
                {
                    return titleElement.GetString() ?? raw;
                }
            }
        }
        catch (JsonException)
        {
            // Fall through and use the raw response payload.
        }

        return raw;
    }

    #endregion
}

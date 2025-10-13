using System.Net.Http;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PackTracker.Application.Constants;
using PackTracker.Domain.Entities;
using PackTracker.Domain.Enums;
using PackTracker.Application.DTOs.Request;
using System.Net.Http.Json;
using System.Windows;

namespace PackTracker.Presentation.ViewModels;

public partial class NewRequestViewModel : ObservableObject
{
    private readonly HttpClient _http = new() { BaseAddress = new Uri("http://localhost:5001/") };

    // Lookup Sources
    public List<LookupOption> SkillObjectiveOptions => RequestFormOptions.SkillObjectives;
    public List<LookupOption> GameBuildOptions => RequestFormOptions.GameBuilds;
    public List<LookupOption> TimeZoneOptions => RequestFormOptions.TimeZones;
    public List<LookupOption> PlatformOptions => RequestFormOptions.PlatformSpecs;
    public List<LookupOption> UrgencyOptions => RequestFormOptions.Urgencies;
    public List<LookupOption> AssetOptions => RequestFormOptions.Assets;
    public List<LookupOption> GroupOptions => RequestFormOptions.GroupSizes;
    public List<LookupOption> RecordOptions => RequestFormOptions.RecordingOptions;

    // Selected Values
    [ObservableProperty] private string? skillObjective;
    [ObservableProperty] private string? gameBuild;
    [ObservableProperty] private string? playerHandle;
    [ObservableProperty] private string? timeZone;
    [ObservableProperty] private string? platformSpecs;
    [ObservableProperty] private string? availability;
    [ObservableProperty] private string? baseline;
    [ObservableProperty] private string? assets;
    [ObservableProperty] private string? urgency;
    [ObservableProperty] private string? groupPreference;
    [ObservableProperty] private string? successCriteria;
    [ObservableProperty] private string? recordingPermission;
    [ObservableProperty] private bool hasMic;

    [RelayCommand]
    private async Task SubmitAsync()
    {
        try
        {
            var dto = new RequestCreateDto
            {
                Title = $"Assistance: {SkillObjective}",
                Description = $"Skill/Objective: {SkillObjective}\n" +
                              $"Game Build: {GameBuild}\n" +
                              $"Player: {PlayerHandle}\n" +
                              $"Time Zone: {TimeZone}\n" +
                              $"Mic/VOIP: {(HasMic ? "Yes" : "No")}\n" +
                              $"Platform/Specs: {PlatformSpecs}\n" +
                              $"Availability: {Availability}\n" +
                              $"Current Baseline: {Baseline}\n" +
                              $"Assets/Ships: {Assets}\n" +
                              $"Urgency: {Urgency}\n" +
                              $"Group Size: {GroupPreference}\n" +
                              $"Success Criteria: {SuccessCriteria}\n" +
                              $"Recording Permission: {RecordingPermission}",
                SkillObjective = SkillObjective ?? "",
                GameBuild = GameBuild ?? "",
                PlayerHandle = PlayerHandle ?? "",
                TimeZone = TimeZone ?? "",
                HasMic = HasMic,
                PlatformSpecs = PlatformSpecs ?? "",
                Availability = Availability ?? "",
                CurrentBaseline = Baseline ?? "",
                AssetsShips = Assets ?? "",
                Urgency = Urgency ?? "",
                GroupPreference = GroupPreference ?? "",
                SuccessCriteria = SuccessCriteria ?? "",
                RecordingPermission = RecordingPermission ?? "",
                Kind = RequestKind.Guidance,
                Priority = RequestPriority.Normal
            };

            var response = await _http.PostAsJsonAsync("api/v1/requests", dto);
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException(await response.Content.ReadAsStringAsync());

            MessageBox.Show("✅ Request submitted successfully!", "PackTracker",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to submit request:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}

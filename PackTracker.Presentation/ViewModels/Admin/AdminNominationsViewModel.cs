using System.Collections.ObjectModel;
using PackTracker.Application.Admin.DTOs;
using PackTracker.Presentation.Services.Admin;

namespace PackTracker.Presentation.ViewModels.Admin;

public sealed class AdminNominationsViewModel : ViewModelBase
{
    private readonly AdminApiClient _api;
    private MedalNominationDto? _selectedNomination;

    public ObservableCollection<MedalNominationDto> PendingNominations { get; } = new();
    public ObservableCollection<MedalNominationDto> HistoryNominations { get; } = new();

    public IReadOnlyList<AdminMemberListItemDto> Members { get; private set; } = Array.Empty<AdminMemberListItemDto>();
    public IReadOnlyList<AdminMedalDefinitionDto> Medals { get; private set; } = Array.Empty<AdminMedalDefinitionDto>();

    public MedalNominationDto? SelectedNomination
    {
        get => _selectedNomination;
        set => SetProperty(ref _selectedNomination, value);
    }

    public AdminNominationsViewModel(AdminApiClient api) { _api = api; }

    public async Task LoadAsync()
    {
        var nominationsTask = _api.GetNominationsAsync();
        var membersTask = _api.GetMembersAsync();
        var medalsTask = _api.GetMedalsAsync();

        await Task.WhenAll(nominationsTask, membersTask, medalsTask);

        var all = await nominationsTask;
        Members = await membersTask;
        var medalsDto = await medalsTask;
        Medals = medalsDto.AvailableMedals;

        PendingNominations.Clear();
        HistoryNominations.Clear();
        foreach (var n in all)
        {
            if (n.Status == "Pending") PendingNominations.Add(n);
            else HistoryNominations.Add(n);
        }
    }

    public async Task<MedalNominationDto?> SubmitAsync(SubmitMedalNominationRequestDto request)
    {
        var result = await _api.SubmitNominationAsync(request);
        if (result is not null) await LoadAsync();
        return result;
    }

    public async Task<bool> ApproveAsync(Guid id, string? notes)
    {
        var result = await _api.ApproveNominationAsync(id, new ReviewMedalNominationRequestDto(notes));
        if (result is not null) { await LoadAsync(); return true; }
        return false;
    }

    public async Task<bool> DenyAsync(Guid id, string? notes)
    {
        var result = await _api.DenyNominationAsync(id, new ReviewMedalNominationRequestDto(notes));
        if (result is not null) { await LoadAsync(); return true; }
        return false;
    }
}

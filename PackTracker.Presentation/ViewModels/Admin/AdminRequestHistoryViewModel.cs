using System.Collections.ObjectModel;
using PackTracker.Application.Admin.DTOs;
using PackTracker.Presentation.Services.Admin;

namespace PackTracker.Presentation.ViewModels.Admin;

public sealed class AdminRequestHistoryViewModel : ViewModelBase
{
    private readonly AdminApiClient _api;
    private string _selectedHistoryType = "Assistance";
    private string _statusMessage = "No history loaded.";
    private AdminRequestHistoryItemDto? _selectedItem;

    public ObservableCollection<string> HistoryTypes { get; } = ["Assistance", "Crafting", "Procurement"];
    public ObservableCollection<AdminRequestHistoryItemDto> Items { get; } = new();

    public AdminRequestHistoryItemDto? SelectedItem
    {
        get => _selectedItem;
        set => SetProperty(ref _selectedItem, value);
    }

    public string SelectedHistoryType
    {
        get => _selectedHistoryType;
        set
        {
            if (!SetProperty(ref _selectedHistoryType, value))
            {
                return;
            }

            _ = LoadAsync();
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public AdminRequestHistoryViewModel(AdminApiClient api)
    {
        _api = api;
    }

    public async Task LoadAsync()
    {
        IReadOnlyList<AdminRequestHistoryItemDto> items = SelectedHistoryType switch
        {
            "Crafting" => await _api.GetCraftingRequestHistoryAsync(),
            "Procurement" => await _api.GetProcurementRequestHistoryAsync(),
            _ => await _api.GetAssistanceRequestHistoryAsync()
        };

        Items.Clear();
        foreach (var item in items)
        {
            Items.Add(item);
        }

        StatusMessage = $"Loaded {Items.Count} {SelectedHistoryType.ToLowerInvariant()} history items.";
    }
}

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PackTracker.Domain.Entities;
using PackTracker.Presentation.Services;
using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using PackTracker.Application.DTOS.Request;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Enums;

namespace PackTracker.Presentation.ViewModels;

public partial class RequestsBoardViewModel : ObservableObject
{
    private readonly IRequestsService _service;
    private readonly ILogger<RequestsBoardViewModel> _logger;

    [ObservableProperty] private ObservableCollection<RequestDto> _items = new();
    [ObservableProperty] private RequestDto? _selected;
    [ObservableProperty] private RequestStatus? _filterStatus = RequestStatus.Open;
    [ObservableProperty] private RequestKind? _filterKind = null;
    [ObservableProperty] private bool _filterMine = false;
    [ObservableProperty] private bool _isBusy = false;
    [ObservableProperty] private string _status = "Ready.";

    public IAsyncRelayCommand LoadCommand { get; }
    public IAsyncRelayCommand CreateCommand { get; }
    public IAsyncRelayCommand<RequestDto> CompleteCommand { get; }
    public IAsyncRelayCommand<RequestDto> DeleteCommand { get; }

    public RequestsBoardViewModel(IRequestsService service, ILogger<RequestsBoardViewModel> logger)
    {
        _service = service;
        _logger = logger;

        LoadCommand = new AsyncRelayCommand(LoadAsync);
        CreateCommand = new AsyncRelayCommand(CreateAsync);
        CompleteCommand = new AsyncRelayCommand<RequestDto>(CompleteAsync);
        DeleteCommand = new AsyncRelayCommand<RequestDto>(DeleteAsync);
    }

    private async Task LoadAsync()
    {
        try
        {
            IsBusy = true;
            Status = "Loading requests...";
            var list = await _service.QueryAsync(_filterStatus, _filterKind, _filterMine, 100);
            Items = new ObservableCollection<RequestDto>(list);
            Status = $"Loaded {Items.Count} requests.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading requests.");
            Status = "Error loading requests.";
        }
        finally { IsBusy = false; }
    }

    private async Task CreateAsync()
    {
        try
        {
            IsBusy = true;
            // For now, quick create (replace with a dialog)
            var dto = new RequestCreateDto
            {
                Title = "New Request",
                Description = "Describe your need…",
                Kind = RequestKind.ResourceGather,
                Priority = RequestPriority.Normal
            };
            var created = await _service.CreateAsync(dto);
            if (created != null) Items.Insert(0, created);
        }
        finally { IsBusy = false; }
    }

    private async Task CompleteAsync(RequestDto? item)
    {
        if (item == null || item.Status == RequestStatus.Completed) return;
        var updated = await _service.CompleteAsync(item.Id);
        if (updated == null) return;
        var idx = Items.IndexOf(item);
        if (idx >= 0) Items[idx] = updated;
    }

    private async Task DeleteAsync(RequestDto? item)
    {
        if (item == null) return;
        if (await _service.DeleteAsync(item.Id))
            Items.Remove(item);
    }
}

using PackTracker.Application.Admin.DTOs;
using PackTracker.Presentation.Services.Admin;

namespace PackTracker.Presentation.ViewModels.Admin;

public sealed class AdminRequestDetailViewModel : ViewModelBase
{
    private readonly AdminApiClient _api;
    private AdminRequestDetailDto? _detail;
    private bool _isLoading;
    private string? _errorMessage;

    public AdminRequestDetailDto? Detail
    {
        get => _detail;
        private set => SetProperty(ref _detail, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public AdminRequestDetailViewModel(AdminApiClient api)
    {
        _api = api;
    }

    public async Task LoadAsync(Guid id, string requestType)
    {
        IsLoading = true;
        ErrorMessage = null;
        Detail = null;

        try
        {
            Detail = await _api.GetRequestDetailAsync(id, requestType);
            if (Detail is null)
                ErrorMessage = "Request not found.";
        }
        finally
        {
            IsLoading = false;
        }
    }
}

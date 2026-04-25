using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Threading;
using PackTracker.Presentation.Services;

namespace PackTracker.Presentation.ViewModels;

public sealed class DiscordEventsViewModel : ViewModelBase
{
    private readonly DiscordEventsService _service;
    private readonly DispatcherTimer _countdownTimer;
    private readonly DispatcherTimer _refreshTimer;
    private bool _isLoading;
    private bool _hasError;
    private string? _errorMessage;

    public DiscordEventsViewModel(DiscordEventsService service)
    {
        _service = service;
        Events = new ObservableCollection<DiscordEventViewModel>();

        _countdownTimer = new DispatcherTimer(
            DispatcherPriority.Normal,
            System.Windows.Application.Current.Dispatcher)
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _countdownTimer.Tick += (_, _) => TickCountdowns();

        _refreshTimer = new DispatcherTimer(
            DispatcherPriority.Normal,
            System.Windows.Application.Current.Dispatcher)
        {
            Interval = TimeSpan.FromMinutes(5)
        };
        _refreshTimer.Tick += (_, _) => _ = RefreshAsync();
    }

    public ObservableCollection<DiscordEventViewModel> Events { get; }

    public bool HasEvents => Events.Count > 0;

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    public bool HasError
    {
        get => _hasError;
        private set => SetProperty(ref _hasError, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public async Task InitializeAsync()
    {
        await RefreshAsync();
        _countdownTimer.Start();
        _refreshTimer.Start();
    }

    public async Task RefreshAsync()
    {
        IsLoading = true;
        HasError = false;
        ErrorMessage = null;
        try
        {
            var items = await _service.GetUpcomingEventsAsync();

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                Events.Clear();
                foreach (var item in items)
                {
                    var vm = new DiscordEventViewModel(item);
                    vm.UpdateCountdown();
                    Events.Add(vm);
                }
                OnPropertyChanged(nameof(HasEvents));
            });
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void TickCountdowns()
    {
        foreach (var e in Events)
            e.UpdateCountdown();
    }
}

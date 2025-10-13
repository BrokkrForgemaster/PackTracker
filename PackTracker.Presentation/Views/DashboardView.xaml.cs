using System.Net.Http;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using PackTracker.Application.Interfaces;
using PackTracker.Presentation.ViewModels;

namespace PackTracker.Presentation.Views;

public partial class DashboardView : UserControl
{
    private readonly IKillEventService _killEventService;

    public DashboardView(IKillEventService killEventService)
    {
        _killEventService = killEventService;
        InitializeComponent();
        DataContext = new DashboardViewModel(killEventService);
    }
}
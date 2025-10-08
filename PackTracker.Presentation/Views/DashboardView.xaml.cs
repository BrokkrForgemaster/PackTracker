using System.Net.Http;
using System.Windows.Controls;
using PackTracker.Application.Interfaces;
using PackTracker.Presentation.ViewModels;

namespace PackTracker.Presentation.Views;

public partial class DashboardView : UserControl
{
    private readonly IKillEventService _killEventService;

    public DashboardView()
    {
        InitializeComponent();
        DataContext = new DashboardViewModel(_killEventService);
    }
}
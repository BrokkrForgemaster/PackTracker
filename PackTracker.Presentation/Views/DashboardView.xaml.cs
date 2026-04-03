using System.Windows.Controls;
using PackTracker.Application.Interfaces;
using PackTracker.Presentation.ViewModels;
using PackTracker.Presentation.Services;

namespace PackTracker.Presentation.Views;

public partial class DashboardView : UserControl
{
    public DashboardView(
        IKillEventService killEventService,
        GuideDashboardViewModel guideViewModel,
        IRegolithService regolithService,
        IApiClientProvider apiClientProvider)
    {
        InitializeComponent();
        DataContext = new DashboardViewModel(killEventService, guideViewModel, regolithService, apiClientProvider);
    }
}

using System.Windows.Controls;
using PackTracker.Presentation.ViewModels;
using PackTracker.Presentation.Services;

namespace PackTracker.Presentation.Views;

public partial class DashboardView : UserControl
{
    public DashboardView(
        GuideDashboardViewModel guideViewModel,
        IApiClientProvider apiClientProvider)
    {
        InitializeComponent();
        DataContext = new DashboardViewModel(guideViewModel, apiClientProvider);
    }
}

using System.Windows.Controls;
using PackTracker.Presentation.ViewModels;

namespace PackTracker.Presentation.Views;

public partial class RefineryJobsView : UserControl
{
    public RefineryJobsView()
    {
        InitializeComponent();
        DataContext = App.GetService<RefineryJobsViewModel>();
    }
}


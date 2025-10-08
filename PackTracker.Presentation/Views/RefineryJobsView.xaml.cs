using System.Windows.Controls;
using PackTracker.Application.Interfaces;
using PackTracker.Presentation.ViewModels;

namespace PackTracker.Presentation.Views;

public class RefineryJobsView : UserControl
{
    private IRegolithService service = App.GetService<IRegolithService>();

    public RefineryJobsView()
    {
        InitializeComponent();
        DataContext = new RefineryJobsViewModel(service);
    }
}
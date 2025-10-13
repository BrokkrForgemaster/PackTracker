using System.Net.Http;
using System.Windows.Controls;
using PackTracker.Application.Interfaces;
using PackTracker.Infrastructure.Services;
using PackTracker.Presentation.ViewModels;

namespace PackTracker.Presentation.Views;

public partial class RefineryJobsView : UserControl
{
    private IRegolithService service { get; }

    public RefineryJobsView()
    {
        InitializeComponent();
        DataContext = new RefineryJobsViewModel(service);
    }
}
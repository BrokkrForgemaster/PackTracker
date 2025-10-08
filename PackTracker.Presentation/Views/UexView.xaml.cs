using System.Windows.Controls;
using PackTracker.Presentation.ViewModels;

namespace PackTracker.Presentation.Views
{
    public partial class UexView : UserControl
    {
        public UexView(UexViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
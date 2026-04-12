using System.Windows;
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

        private void Help_Click(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is MainWindow mw)
            {
                mw.ShowHelp("Trading Hub", "trading-hub");
            }
        }
    }
}
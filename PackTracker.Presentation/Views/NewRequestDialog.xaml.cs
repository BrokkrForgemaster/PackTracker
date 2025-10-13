using System.Windows;
using PackTracker.Presentation.ViewModels;

namespace PackTracker.Presentation.Views;

public partial class NewRequestDialog : Window
{
    public NewRequestDialog()
    {
        InitializeComponent();
        DataContext = new NewRequestViewModel();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
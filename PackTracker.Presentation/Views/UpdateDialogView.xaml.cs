using System.Windows.Controls;
using System.Windows.Input;
using PackTracker.Presentation.ViewModels;

namespace PackTracker.Presentation.Views;

public partial class UpdateDialogView : UserControl
{
    public UpdateDialogView()
    {
        InitializeComponent();
    }

    private void Backdrop_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is UpdateNotificationViewModel vm)
        {
            vm.IsDialogOpen = false;
        }
    }

    private void Card_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Swallow clicks inside the card so they don't bubble up to the backdrop and dismiss the dialog.
        e.Handled = true;
    }
}

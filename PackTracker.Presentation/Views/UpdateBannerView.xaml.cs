using System.Windows.Controls;
using System.Windows.Input;
using PackTracker.Presentation.ViewModels;

namespace PackTracker.Presentation.Views;

public partial class UpdateBannerView : UserControl
{
    public UpdateBannerView()
    {
        InitializeComponent();
    }

    private void BannerRoot_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is UpdateNotificationViewModel vm
            && vm.OpenDialogCommand.CanExecute(null))
        {
            vm.OpenDialogCommand.Execute(null);
        }
    }
}

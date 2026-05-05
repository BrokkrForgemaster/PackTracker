using System.Windows;
using System.Windows.Controls;
using PackTracker.Presentation.ViewModels.Admin;

namespace PackTracker.Presentation.Views.Admin;

public partial class AdminAuditLogsView : UserControl
{
    public AdminAuditLogsView(AdminAuditLogsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private async void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is AdminAuditLogsViewModel vm)
        {
            await vm.LoadLogsAsync();
        }
    }
}

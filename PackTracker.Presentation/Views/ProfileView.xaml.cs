using System.Windows.Controls;
using PackTracker.Presentation.ViewModels;

namespace PackTracker.Presentation.Views;

public partial class ProfileView : UserControl
{
    public ProfileView(ProfileViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        Loaded += async (_, _) =>
        {
            await viewModel.LoadAsync();
        };
    }
}
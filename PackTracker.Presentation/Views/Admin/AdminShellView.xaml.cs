using System.Windows.Controls;
using PackTracker.Presentation.Services.Navigation;
using PackTracker.Presentation.ViewModels.Admin;

namespace PackTracker.Presentation.Views.Admin;

public partial class AdminShellView : UserControl
{
    private readonly AdminShellViewModel _viewModel;
    private readonly AdminDashboardView _dashboardView;
    private readonly AdminSettingsView _settingsView;
    private readonly AdminMembersView _membersView;
    private readonly AdminMedalsView _medalsView;
    private readonly AdminRecruitmentView _recruitmentView;
    private readonly AdminRequestHistoryView _requestHistoryView;
    private readonly NavigationStateService _navigationState;

    public AdminShellView(
        AdminShellViewModel viewModel,
        AdminDashboardView dashboardView,
        AdminSettingsView settingsView,
        AdminMembersView membersView,
        AdminMedalsView medalsView,
        AdminRecruitmentView recruitmentView,
        AdminRequestHistoryView requestHistoryView,
        NavigationStateService navigationState)
    {
        InitializeComponent();
        DataContext = _viewModel = viewModel;
        _dashboardView = dashboardView;
        _settingsView = settingsView;
        _membersView = membersView;
        _medalsView = medalsView;
        _recruitmentView = recruitmentView;
        _requestHistoryView = requestHistoryView;
        _navigationState = navigationState;

        ShowDashboard();
    }

    public void SetTierLabel(string? tierLabel)
    {
        _viewModel.AdminTierLabel = string.IsNullOrWhiteSpace(tierLabel) ? "Admin" : tierLabel;
    }

    private void Dashboard_Click(object sender, System.Windows.RoutedEventArgs e) => ShowDashboard();

    private void Settings_Click(object sender, System.Windows.RoutedEventArgs e) => ShowSettings();

    private void Members_Click(object sender, System.Windows.RoutedEventArgs e) => ShowMembers();

    private void Medals_Click(object sender, System.Windows.RoutedEventArgs e) => ShowMedals();

    private void Recruitment_Click(object sender, System.Windows.RoutedEventArgs e) => ShowRecruitment();

    private void RequestHistory_Click(object sender, System.Windows.RoutedEventArgs e) => ShowRequestHistory();

    private void Back_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (System.Windows.Window.GetWindow(this) is MainWindow mainWindow)
        {
            mainWindow.ReturnFromAdmin();
        }
    }

    private void ShowDashboard()
    {
        _viewModel.CurrentSection = "Dashboard";
        _navigationState.CaptureAdminView("Dashboard");
        AdminContentHost.Content = _dashboardView;
    }

    private void ShowSettings()
    {
        _viewModel.CurrentSection = "Settings";
        _navigationState.CaptureAdminView("Settings");
        AdminContentHost.Content = _settingsView;
    }

    private void ShowMembers()
    {
        _viewModel.CurrentSection = "Members";
        _navigationState.CaptureAdminView("Members");
        AdminContentHost.Content = _membersView;
    }

    private void ShowMedals()
    {
        _viewModel.CurrentSection = "Medals";
        _navigationState.CaptureAdminView("Medals");
        AdminContentHost.Content = _medalsView;
    }

    private void ShowRecruitment()
    {
        _viewModel.CurrentSection = "Recruitment";
        _navigationState.CaptureAdminView("Recruitment");
        AdminContentHost.Content = _recruitmentView;
    }

    private void ShowRequestHistory()
    {
        _viewModel.CurrentSection = "Request History";
        _navigationState.CaptureAdminView("RequestHistory");
        AdminContentHost.Content = _requestHistoryView;
    }
}

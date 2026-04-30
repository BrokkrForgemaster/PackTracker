using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;
using PackTracker.Presentation.ViewModels.Admin;

namespace PackTracker.Presentation.Views.Admin;

public partial class AdminRecruitmentView : UserControl
{
    private readonly AdminRecruitmentViewModel _viewModel;
    private bool _webViewReady;

    public AdminRecruitmentView(AdminRecruitmentViewModel viewModel)
    {
        InitializeComponent();
        DataContext = _viewModel = viewModel;

        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AdminRecruitmentViewModel.HtmlPreview))
                Dispatcher.Invoke(RefreshPreview);
        };

        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await WebView.EnsureCoreWebView2Async();

            var assetsPath = Path.Combine(AppContext.BaseDirectory, "Assets");
            WebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                "assets.packtracker.local",
                assetsPath,
                CoreWebView2HostResourceAccessKind.Allow);

            _webViewReady = true;
            _viewModel.WebViewAvailable = true;
            RefreshPreview();
        }
        catch
        {
            _viewModel.WebViewAvailable = false;
            _viewModel.StatusMessage = "WebView2 unavailable — install the Edge WebView2 Runtime for the rich preview.";
        }
    }

    private void RefreshPreview()
    {
        if (_webViewReady)
            WebView.NavigateToString(_viewModel.HtmlPreview);
    }
}

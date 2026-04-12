using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;

namespace PackTracker.Presentation.Views;

public partial class HelpView : UserControl
{
    private readonly string _guidePath;
    private string? _targetAnchor;

    public HelpView()
    {
        InitializeComponent();
        _guidePath = Path.Combine(AppContext.BaseDirectory, "wwwroot", "UserGuide.md");
        InitializeAsync();
    }

    private async void InitializeAsync()
    {
        await WebView.EnsureCoreWebView2Async();
        
        // Use a simple HTML wrapper to render markdown via a CDN or local script if needed, 
        // but for now we'll just show the text in a nicely styled div.
        LoadContent();
    }

    public void NavigateToSection(string sectionName, string anchor)
    {
        TxtSection.Text = $"Section: {sectionName}";
        _targetAnchor = anchor;
        
        if (WebView.CoreWebView2 != null)
        {
            LoadContent();
        }
    }

    private void LoadContent()
    {
        if (!File.Exists(_guidePath))
        {
            WebView.NavigateToString("<html><body><h1>Guide not found</h1></body></html>");
            return;
        }

        string markdown = File.ReadAllText(_guidePath);
        
        // Simple HTML template with Dark theme styling
        string html = $@"
<html>
<head>
    <style>
        body {{ 
            background-color: #0F0F0F; 
            color: #F5F5F5; 
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; 
            padding: 40px;
            line-height: 1.6;
        }}
        h1, h2 {{ color: #CC5500; border-bottom: 1px solid #2F2F2F; padding-bottom: 10px; margin-top: 30px; }}
        h3 {{ color: #C2A23A; }}
        code {{ background-color: #1E1E1E; padding: 2px 4px; border-radius: 4px; }}
        ul {{ padding-left: 20px; }}
        li {{ margin-bottom: 8px; }}
        hr {{ border: 0; border-top: 1px solid #2F2F2F; margin: 40px 0; }}
        .anchor {{ margin-top: -100px; padding-top: 100px; }}
    </style>
</head>
<body>
    <div id='content'>
        {ConvertMarkdownToSimpleHtml(markdown)}
    </div>
    <script>
        if ('{_targetAnchor}') {{
            const el = document.getElementsByName('{_targetAnchor}')[0];
            if (el) el.scrollIntoView();
        }}
    </script>
</body>
</html>";

        WebView.NavigateToString(html);
    }

    private string ConvertMarkdownToSimpleHtml(string md)
    {
        // Very basic conversion for the prototype - in production use a real MD library
        return md
            .Replace("#### ", "<h5>")
            .Replace("### ", "<h4>")
            .Replace("## ", "<h3>")
            .Replace("**", "<b>")
            .Replace("\n* ", "<li>")
            .Replace("\n", "<br/>")
            .Replace("<h4><br/>", "<h4>")
            .Replace("<h3><br/>", "<h3>")
            .Replace("<h2><br/>", "<h2>");
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is MainWindow mw)
        {
            mw.HideHelp();
        }
    }
}

using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Extensions.DependencyInjection;

namespace PackTracker.Mobile;

#pragma warning disable CA1724 // The type name conflicts in whole or in part with the namespace name
public partial class App : Microsoft.Maui.Controls.Application
#pragma warning restore CA1724
{
    private readonly IServiceProvider _services;

    public App(IServiceProvider services)
    {
        InitializeComponent();
        _services = services;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        try
        {
            var shell = _services.GetRequiredService<AppShell>();
            return new Window(shell);
        }
        catch (Exception ex)
        {
            var fallbackPage = new ContentPage
            {
                Title = "Startup Error",
                BackgroundColor = Color.FromArgb("#0E0B08"),
                Content = new ScrollView
                {
                    Content = new VerticalStackLayout
                    {
                        Padding = 24,
                        Spacing = 12,
                        Children =
                        {
                            new Label
                            {
                                Text = "PackTracker failed to start.",
                                TextColor = Color.FromArgb("#D6A04B"),
                                FontAttributes = FontAttributes.Bold,
                                FontSize = 20
                            },
                            new Label
                            {
                                Text = ex.GetType().Name + ": " + ex.Message,
                                TextColor = Color.FromArgb("#F4EBDD"),
                                FontSize = 13
                            },
                            new Label
                            {
                                Text = ex.StackTrace ?? string.Empty,
                                TextColor = Color.FromArgb("#B8A88A"),
                                FontSize = 10
                            }
                        }
                    }
                }
            };

            return new Window(fallbackPage);
        }
    }
}

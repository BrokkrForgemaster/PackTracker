using Microsoft.Maui;
using Microsoft.Maui.Controls;

namespace PackTracker.Mobile;

#pragma warning disable CA1724 // The type name conflicts in whole or in part with the namespace name
public partial class App : Microsoft.Maui.Controls.Application
#pragma warning restore CA1724
{
    private readonly AppShell _shell;

    public App(AppShell shell)
    {
        InitializeComponent();
        _shell = shell;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(_shell);
    }
}
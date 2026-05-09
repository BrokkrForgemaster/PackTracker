using PackTracker.SharedPresentation.Responsive;

namespace PackTracker.Mobile.Layout;

public partial class ResponsiveShellLayout : ContentView
{
    private readonly IResponsiveLayoutService _responsive;

    public ResponsiveShellLayout(IResponsiveLayoutService responsive)
    {
        _responsive = responsive;
        InitializeComponent();
        SizeChanged += OnSizeChanged;
    }

    private void OnSizeChanged(object? sender, EventArgs e)
    {
        var state = _responsive.Compute(Width, Height);
        // Layout state available for consumer binding or code-behind adjustments
        BindingContext = state;
    }
}

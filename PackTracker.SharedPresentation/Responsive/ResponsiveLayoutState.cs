namespace PackTracker.SharedPresentation.Responsive;

public sealed class ResponsiveLayoutState
{
    public double Width { get; init; }
    public double Height { get; init; }
    public ResponsiveBreakpoint Breakpoint { get; init; }
    public bool IsCompact => Breakpoint == ResponsiveBreakpoint.Compact;
    public bool IsMedium => Breakpoint == ResponsiveBreakpoint.Medium;
    public bool IsExpanded => Breakpoint == ResponsiveBreakpoint.Expanded;
    public bool UseBottomNavigation { get; init; }
    public bool UseDrawerNavigation { get; init; }
    public bool UseSidebarNavigation { get; init; }
    public int ContentColumnCount { get; init; }
}

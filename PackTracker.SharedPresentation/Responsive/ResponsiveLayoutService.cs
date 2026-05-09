namespace PackTracker.SharedPresentation.Responsive;

public sealed class ResponsiveLayoutService : IResponsiveLayoutService
{
    public ResponsiveLayoutState Compute(double width, double height)
    {
        var breakpoint = width < 600
            ? ResponsiveBreakpoint.Compact
            : width < 1024
                ? ResponsiveBreakpoint.Medium
                : ResponsiveBreakpoint.Expanded;

        return breakpoint switch
        {
            ResponsiveBreakpoint.Compact => new ResponsiveLayoutState
            {
                Width = width,
                Height = height,
                Breakpoint = ResponsiveBreakpoint.Compact,
                UseBottomNavigation = true,
                UseDrawerNavigation = false,
                UseSidebarNavigation = false,
                ContentColumnCount = 1
            },
            ResponsiveBreakpoint.Medium => new ResponsiveLayoutState
            {
                Width = width,
                Height = height,
                Breakpoint = ResponsiveBreakpoint.Medium,
                UseBottomNavigation = false,
                UseDrawerNavigation = true,
                UseSidebarNavigation = false,
                ContentColumnCount = 1
            },
            _ => new ResponsiveLayoutState
            {
                Width = width,
                Height = height,
                Breakpoint = ResponsiveBreakpoint.Expanded,
                UseBottomNavigation = false,
                UseDrawerNavigation = false,
                UseSidebarNavigation = true,
                ContentColumnCount = 2
            }
        };
    }
}

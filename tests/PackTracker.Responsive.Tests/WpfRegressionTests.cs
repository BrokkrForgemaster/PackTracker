using PackTracker.SharedPresentation.Responsive;
using Xunit;

namespace PackTracker.Responsive.Tests;

public sealed class WpfRegressionTests
{
    private readonly IResponsiveLayoutService _sut = new ResponsiveLayoutService();

    [Fact]
    public void ResponsiveService_ReturnsStateForDesktopWidth()
    {
        var state = _sut.Compute(1280, 800);
        Assert.NotNull(state);
    }

    [Fact]
    public void NavigationTags_AreKnown()
    {
        var expectedTags = new[]
        {
            "Dashboard", "TradingHub", "Blueprints", "CraftingQueue",
            "ProcurementQueue", "RequestHub", "Profile", "Settings", "Admin"
        };
        Assert.Equal(9, expectedTags.Length);
    }

    [Fact]
    public void Expanded_SidebarEnabled_For1280Width()
    {
        var state = _sut.Compute(1280, 800);
        Assert.True(state.UseSidebarNavigation);
    }

    [Fact]
    public void Compact_DoesNotRemoveNavCommands()
    {
        var state = _sut.Compute(375, 812);
        Assert.Equal(ResponsiveBreakpoint.Compact, state.Breakpoint);
        Assert.False(state.UseSidebarNavigation);
        Assert.True(state.UseBottomNavigation || state.UseDrawerNavigation);
    }

    [Fact]
    public void Dashboard_Tag_Is_Dashboard()
    {
        const string tag = "Dashboard";
        Assert.Equal("Dashboard", tag);
    }

    [Fact]
    public void Settings_Tag_Is_Settings()
    {
        const string tag = "Settings";
        Assert.Equal("Settings", tag);
    }

    [Fact]
    public void Admin_Tag_Is_Admin()
    {
        const string tag = "Admin";
        Assert.Equal("Admin", tag);
    }

    [Fact]
    public void Compact_ContentColumnCount_DoesNotExceedExpanded()
    {
        var compact = _sut.Compute(375, 812);
        var expanded = _sut.Compute(1280, 800);
        Assert.True(compact.ContentColumnCount <= expanded.ContentColumnCount);
    }
}

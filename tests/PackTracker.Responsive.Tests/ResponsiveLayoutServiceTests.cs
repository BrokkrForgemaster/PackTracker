using PackTracker.SharedPresentation.Responsive;
using Xunit;

namespace PackTracker.Responsive.Tests;

public sealed class ResponsiveLayoutServiceTests
{
    private readonly IResponsiveLayoutService _sut = new ResponsiveLayoutService();

    [Fact]
    public void Width375_Returns_Compact()
    {
        var state = _sut.Compute(375, 812);
        Assert.Equal(ResponsiveBreakpoint.Compact, state.Breakpoint);
    }

    [Fact]
    public void Width599_Returns_Compact()
    {
        var state = _sut.Compute(599, 812);
        Assert.Equal(ResponsiveBreakpoint.Compact, state.Breakpoint);
    }

    [Fact]
    public void Width600_Returns_Medium()
    {
        var state = _sut.Compute(600, 800);
        Assert.Equal(ResponsiveBreakpoint.Medium, state.Breakpoint);
    }

    [Fact]
    public void Width768_Returns_Medium()
    {
        var state = _sut.Compute(768, 1024);
        Assert.Equal(ResponsiveBreakpoint.Medium, state.Breakpoint);
    }

    [Fact]
    public void Width1023_Returns_Medium()
    {
        var state = _sut.Compute(1023, 768);
        Assert.Equal(ResponsiveBreakpoint.Medium, state.Breakpoint);
    }

    [Fact]
    public void Width1024_Returns_Expanded()
    {
        var state = _sut.Compute(1024, 768);
        Assert.Equal(ResponsiveBreakpoint.Expanded, state.Breakpoint);
    }

    [Fact]
    public void Compact_DisablesSidebarNavigation()
    {
        var state = _sut.Compute(375, 812);
        Assert.False(state.UseSidebarNavigation);
    }

    [Fact]
    public void Expanded_EnablesSidebarNavigation()
    {
        var state = _sut.Compute(1280, 800);
        Assert.True(state.UseSidebarNavigation);
    }

    [Fact]
    public void Compact_ContentColumnCount_Is1()
    {
        var state = _sut.Compute(375, 812);
        Assert.Equal(1, state.ContentColumnCount);
    }

    [Fact]
    public void Expanded_ContentColumnCount_IsAtLeast2()
    {
        var state = _sut.Compute(1280, 800);
        Assert.True(state.ContentColumnCount >= 2);
    }
}

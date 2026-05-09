namespace PackTracker.SharedPresentation.Responsive;

public interface IResponsiveLayoutService
{
    ResponsiveLayoutState Compute(double width, double height);
}

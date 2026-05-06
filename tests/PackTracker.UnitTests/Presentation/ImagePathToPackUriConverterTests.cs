using System.Windows.Media.Imaging;
using PackTracker.Presentation.Converters;

namespace PackTracker.UnitTests.Presentation;

public sealed class ImagePathToPackUriConverterTests
{
    [Fact]
    public void TrimTransparentBounds_CropsMineOrDieRibbonPadding()
    {
        var imagePath = Path.Combine(
            GetRepositoryRoot(),
            "PackTracker.Presentation",
            "Assets",
            "ribbons",
            "mineordiemissionribbon.png");

        var source = new BitmapImage(new Uri(
            imagePath,
            UriKind.Absolute));

        var trimmed = ImagePathToPackUriConverter.TrimTransparentBounds(source);

        Assert.Equal(299, source.PixelWidth);
        Assert.Equal(205, source.PixelHeight);
        Assert.True(trimmed.PixelWidth < source.PixelWidth);
        Assert.True(trimmed.PixelHeight < source.PixelHeight);
    }

    [Fact]
    public void NormalizePath_RemovesLeadingRelativeSegments()
    {
        var normalized = ImagePathToPackUriConverter.NormalizePath(@"..\Assets\ribbons\mineordiemissionribbon.png");

        Assert.Equal("Assets/ribbons/mineordiemissionribbon.png", normalized);
    }

    private static string GetRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Directory.Build.props")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root from test base directory.");
    }
}

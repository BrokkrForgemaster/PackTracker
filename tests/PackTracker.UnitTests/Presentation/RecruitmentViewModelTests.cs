using PackTracker.Domain.Enums;
using PackTracker.Presentation.ViewModels;

namespace PackTracker.UnitTests.Presentation;

public sealed class RecruitmentViewModelTests
{
    [Fact]
    public void DefaultCopyModes_DoNotExposeLegacyBbCode()
    {
        var sut = new RecruitmentViewModel();

        Assert.Contains(RecruitmentCopyMode.Plain, sut.CopyModes);
        Assert.Contains(RecruitmentCopyMode.Html, sut.CopyModes);
        Assert.DoesNotContain(RecruitmentCopyMode.StyledBbCode, sut.CopyModes);
        Assert.Equal(RecruitmentCopyMode.Html, sut.SelectedCopyMode);
    }

    [Fact]
    public void HtmlMode_GeneratesHtmlClipboardPayload_WithImages()
    {
        var sut = new RecruitmentViewModel();

        sut.SelectedCopyMode = RecruitmentCopyMode.Html;

        Assert.Contains("<img", sut.GeneratedPost);
        Assert.Contains("division-icon", sut.GeneratedPost);
        Assert.Contains("<div", sut.GeneratedPost);
        Assert.DoesNotContain("[img]", sut.GeneratedPost);
    }

        [Fact]
    public void PlainMode_GeneratesPlainText_WithoutMarkup()
    {
        var sut = new RecruitmentViewModel
        {
            SelectedCopyMode = RecruitmentCopyMode.Plain
        };

        Assert.Contains("https://i.imgur.com/lD4P6Cv.png?s=32", sut.GeneratedPost);
        Assert.Contains("https://i.imgur.com/HfepOyk.png?s=32", sut.GeneratedPost);
        Assert.Contains("https://i.imgur.com/6rilziI.png?s=32", sut.GeneratedPost);
        Assert.Contains("https://i.imgur.com/TlCNBwW.png?s=32", sut.GeneratedPost);
        Assert.DoesNotContain("[img]", sut.GeneratedPost);
        Assert.DoesNotContain("<div", sut.GeneratedPost);
        Assert.DoesNotContain("<img", sut.GeneratedPost);
    }
}

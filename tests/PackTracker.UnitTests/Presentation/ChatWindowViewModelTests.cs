using System.Windows.Media;
using PackTracker.Presentation.ViewModels;

namespace PackTracker.UnitTests.Presentation;

public class ChatWindowViewModelTests
{
    [Fact]
    public void SendMessage_AddsLocalMessage_ForRegularChannelMessage()
    {
        var sut = CreateWindow();
        sut.ChannelKey = "general";
        sut.Title = "General";
        sut.DraftMessage = "hello team";

        sut.SendMessageCommand.Execute(null);

        Assert.Single(sut.Messages);
        Assert.Equal("You", sut.Messages[0].SenderDisplayName);
        Assert.Equal("hello team", sut.Messages[0].Content);
    }

    [Fact]
    public void SendMessage_DoesNotAddLocalMessage_ForDirectMessageWindow()
    {
        var sut = CreateWindow();
        sut.ChannelKey = "direct:ghost";
        sut.Title = "DM // Ghost";
        sut.TargetUsername = "ghost";
        sut.DraftMessage = "quiet ping";

        sut.SendMessageCommand.Execute(null);

        Assert.Empty(sut.Messages);
    }

    [Fact]
    public void SendMessage_DoesNotAddLocalMessage_ForMentionDirectedMessage()
    {
        var sut = CreateWindow();
        sut.ChannelKey = "general";
        sut.Title = "General";
        sut.DraftMessage = "@ghost meet in ops";

        sut.SendMessageCommand.Execute(null);

        Assert.Empty(sut.Messages);
    }

    [Fact]
    public void SendMessage_RaisesMessageSent_WithTrimmedContent()
    {
        var sut = CreateWindow();
        sut.ChannelKey = "general";
        sut.Title = "General";
        sut.DraftMessage = "  @ghost meet in ops  ";

        string? sentChannel = null;
        string? sentContent = null;
        sut.MessageSent += (channel, content) =>
        {
            sentChannel = channel;
            sentContent = content;
        };

        sut.SendMessageCommand.Execute(null);

        Assert.Equal("general", sentChannel);
        Assert.Equal("@ghost meet in ops", sentContent);
    }

    [Fact]
    public void ContactLabel_IncludesUsername_WhenAvailable()
    {
        var sut = new OnlineUserViewModel
        {
            DisplayName = "Ghost",
            Username = "ghost"
        };

        Assert.Equal("Ghost (@ghost)", sut.ContactLabel);
    }

    private static ChatWindowViewModel CreateWindow()
    {
        return new ChatWindowViewModel(
            _ => { },
            _ => { },
            _ => { })
        {
            AccentBrush = Brushes.Gray
        };
    }
}

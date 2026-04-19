using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace PackTracker.Presentation.Controls;

public class MarkdownTextBlock : TextBlock
{
    public static readonly DependencyProperty MarkdownProperty =
        DependencyProperty.Register(
            nameof(Markdown),
            typeof(string),
            typeof(MarkdownTextBlock),
            new FrameworkPropertyMetadata(string.Empty, OnMarkdownChanged));

    public string Markdown
    {
        get => (string)GetValue(MarkdownProperty);
        set => SetValue(MarkdownProperty, value);
    }

    private static void OnMarkdownChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not MarkdownTextBlock tb) return;

        tb.Inlines.Clear();

        var text = e.NewValue as string;
        if (string.IsNullOrEmpty(text)) return;

        foreach (var inline in DiscordMarkdownParser.Parse(text))
            tb.Inlines.Add(inline);
    }
}

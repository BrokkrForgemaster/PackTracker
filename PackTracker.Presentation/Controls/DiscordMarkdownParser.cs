using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace PackTracker.Presentation.Controls;

public static class DiscordMarkdownParser
{
    private static readonly Brush CodeBackgroundBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0x2B, 0x2D, 0x31));
    private static readonly Brush CodeBlockBackgroundBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0x1E, 0x1F, 0x22));
    private static readonly Brush QuoteBarBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0x4E, 0x50, 0x58));
    private static readonly Brush SpoilerBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0x31, 0x33, 0x38));
    private static readonly FontFamily MonoFont = new FontFamily("Consolas, Courier New");

    public static IEnumerable<Inline> Parse(string? text)
    {
        if (string.IsNullOrEmpty(text))
            yield break;

        // Extract code blocks first (``` ... ```) to prevent inner parsing
        var segments = SplitCodeBlocks(text);

        foreach (var (content, isCodeBlock) in segments)
        {
            if (isCodeBlock)
            {
                yield return BuildCodeBlock(content);
                continue;
            }

            // Split into lines
            var lines = content.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];

                if (i > 0)
                    yield return new LineBreak();

                if (line.StartsWith("> ", StringComparison.Ordinal))
                {
                    yield return BuildBlockQuote(line[2..]);
                }
                else if (line.StartsWith(">", StringComparison.Ordinal) && line.Length == 1)
                {
                    yield return BuildBlockQuote(string.Empty);
                }
                else
                {
                    foreach (var inline in ParseInline(line))
                        yield return inline;
                }
            }
        }
    }

    // Split text into (content, isCodeBlock) segments
    private static List<(string content, bool isCodeBlock)> SplitCodeBlocks(string text)
    {
        var result = new List<(string, bool)>();
        var pattern = new Regex(@"```(?:\w+\n?)?([\s\S]*?)```", RegexOptions.Multiline);
        int lastIndex = 0;

        foreach (Match m in pattern.Matches(text))
        {
            if (m.Index > lastIndex)
                result.Add((text[lastIndex..m.Index], false));

            result.Add((m.Groups[1].Value.Trim(), true));
            lastIndex = m.Index + m.Length;
        }

        if (lastIndex < text.Length)
            result.Add((text[lastIndex..], false));

        return result;
    }

    private static Inline BuildCodeBlock(string code)
    {
        var tb = new TextBlock
        {
            Text = code,
            FontFamily = MonoFont,
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0xDC, 0xDD, 0xDE)),
            TextWrapping = TextWrapping.Wrap,
            Padding = new Thickness(10, 6, 10, 6),
            Margin = new Thickness(0)
        };

        var border = new Border
        {
            Background = CodeBlockBackgroundBrush,
            CornerRadius = new CornerRadius(4),
            Child = tb,
            Margin = new Thickness(0, 4, 0, 4)
        };

        return new InlineUIContainer(border);
    }

    private static Inline BuildBlockQuote(string text)
    {
        var contentTb = new TextBlock
        {
            FontStyle = FontStyles.Italic,
            Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0xB5, 0xBA, 0xC1)),
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center
        };

        foreach (var inline in ParseInline(text))
            contentTb.Inlines.Add(inline);

        var bar = new Border
        {
            Width = 3,
            Background = QuoteBarBrush,
            CornerRadius = new CornerRadius(2),
            Margin = new Thickness(0, 0, 8, 0)
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(bar, 0);
        Grid.SetColumn(contentTb, 1);
        grid.Children.Add(bar);
        grid.Children.Add(contentTb);

        var wrapper = new Border
        {
            Child = grid,
            Margin = new Thickness(0, 2, 0, 2)
        };

        return new InlineUIContainer(wrapper);
    }

    // Parse inline markdown (no code blocks inside here)
    private static IEnumerable<Inline> ParseInline(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            yield return new Run(string.Empty);
            yield break;
        }

        // Ordered: longer/stricter patterns first
        var pattern = new Regex(
            @"(\*\*\*(?<bolditalic>.+?)\*\*\*)" +
            @"|(\*\*(?<bold>.+?)\*\*)" +
            @"|(__(?<underline>.+?)__)" +
            @"|(\*(?<italic>.+?)\*)" +
            @"|(_(?<italic2>[^_]+?)_)" +
            @"|(~~(?<strike>.+?)~~)" +
            @"|(\|\|(?<spoiler>.+?)\|\|)" +
            @"|(`(?<code>[^`]+?)`)",
            RegexOptions.Singleline);

        int lastIndex = 0;
        foreach (Match m in pattern.Matches(text))
        {
            // Plain text before match
            if (m.Index > lastIndex)
                yield return new Run(text[lastIndex..m.Index]);

            if (m.Groups["bolditalic"].Success)
            {
                var r = new Run(m.Groups["bolditalic"].Value);
                yield return new Bold(new Italic(r));
            }
            else if (m.Groups["bold"].Success)
            {
                yield return new Bold(new Run(m.Groups["bold"].Value));
            }
            else if (m.Groups["underline"].Success)
            {
                var r = new Run(m.Groups["underline"].Value);
                r.TextDecorations = TextDecorations.Underline;
                yield return r;
            }
            else if (m.Groups["italic"].Success || m.Groups["italic2"].Success)
            {
                var val = m.Groups["italic"].Success ? m.Groups["italic"].Value : m.Groups["italic2"].Value;
                yield return new Italic(new Run(val));
            }
            else if (m.Groups["strike"].Success)
            {
                var r = new Run(m.Groups["strike"].Value);
                r.TextDecorations = TextDecorations.Strikethrough;
                yield return r;
            }
            else if (m.Groups["spoiler"].Success)
            {
                yield return BuildSpoiler(m.Groups["spoiler"].Value);
            }
            else if (m.Groups["code"].Success)
            {
                yield return BuildInlineCode(m.Groups["code"].Value);
            }

            lastIndex = m.Index + m.Length;
        }

        // Remaining plain text
        if (lastIndex < text.Length)
            yield return new Run(text[lastIndex..]);
    }

    private static Inline BuildInlineCode(string code)
    {
        var tb = new TextBlock
        {
            Text = code,
            FontFamily = MonoFont,
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0xDC, 0xDD, 0xDE)),
            Padding = new Thickness(3, 1, 3, 1),
            VerticalAlignment = VerticalAlignment.Center
        };

        var border = new Border
        {
            Background = CodeBackgroundBrush,
            CornerRadius = new CornerRadius(3),
            Child = tb,
            Padding = new Thickness(0),
            Margin = new Thickness(1, 0, 1, 0)
        };

        return new InlineUIContainer(border) { BaselineAlignment = BaselineAlignment.Center };
    }

    private static Inline BuildSpoiler(string content)
    {
        var tb = new TextBlock
        {
            Text = content,
            Foreground = SpoilerBrush, // hidden by default — same color as bg
            Padding = new Thickness(3, 1, 3, 1),
            VerticalAlignment = VerticalAlignment.Center
        };

        var border = new Border
        {
            Background = SpoilerBrush,
            CornerRadius = new CornerRadius(3),
            Child = tb,
            Cursor = System.Windows.Input.Cursors.Hand,
            Margin = new Thickness(1, 0, 1, 0)
        };

        // Click to reveal
        border.MouseLeftButtonDown += (s, e) =>
        {
            if (s is Border b && b.Child is TextBlock t)
            {
                t.Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0xDC, 0xDD, 0xDE));
                b.Background = new SolidColorBrush(Color.FromArgb(0xFF, 0x31, 0x33, 0x38));
                b.Cursor = System.Windows.Input.Cursors.Arrow;
            }
        };

        return new InlineUIContainer(border) { BaselineAlignment = BaselineAlignment.Center };
    }
}

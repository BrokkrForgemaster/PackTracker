using System.Collections.ObjectModel;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using PackTracker.Domain.Enums;
using PackTracker.Presentation.Commands;

namespace PackTracker.Presentation.ViewModels;

public sealed class RecruitmentDivision : ViewModelBase
{
    private bool _isEnabled = true;
    private string _emoji = string.Empty;
    private string _name = string.Empty;
    private string _tagline = string.Empty;
    private string _description = string.Empty;
    private string _imagePath = string.Empty;

    public bool IsEnabled { get => _isEnabled; set => SetProperty(ref _isEnabled, value); }
    public string Emoji { get => _emoji; set => SetProperty(ref _emoji, value); }
    public string Name { get => _name; set => SetProperty(ref _name, value); }
    public string Tagline { get => _tagline; set => SetProperty(ref _tagline, value); }
    public string Description { get => _description; set => SetProperty(ref _description, value); }
    public string ImagePath { get => _imagePath; set => SetProperty(ref _imagePath, value); }
}

public sealed class RecruitmentViewModel : ViewModelBase
{
    private static readonly Regex BbCodeImgRegex = new(@"\[img\](.+?)\[/img\]", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex BbCodeDivisionLineRegex = new(@"^\[img\](.+?)\[/img\]\s*(.+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex BbCodeUrlRegex = new(@"\[url=(.+?)\](.+?)\[/url\]", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex BbCodeBoldRegex = new(@"\[b\](.+?)\[/b\]", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex BbCodeItalicRegex = new(@"\[i\](.+?)\[/i\]", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex BbCodeCenterRegex = new(@"\[center\](.+?)\[/center\]", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex BbCodeSizeRegex = new(@"\[size=\d+\](.+?)\[/size\]", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private string _orgName = string.Empty;
    private string _headline = string.Empty;
    private string _bannerImagePath = string.Empty;
    private string _openingStatement = string.Empty;
    private string _whyJoinText = string.Empty;
    private string _callToAction = string.Empty;
    private string _websiteUrl = string.Empty;
    private string _rsiUrl = string.Empty;
    private string _discordUrl = string.Empty;
    private string _language = string.Empty;
    private string _signOff = string.Empty;
    private string _generatedPost = string.Empty;
    private string _htmlPreview = string.Empty;
    private string _statusMessage = string.Empty;
    private bool _webViewAvailable = true;
    private bool _showWhyJoin = true;
    private bool _showDivisions = true;
    private string _validationWarning = string.Empty;
    private RecruitmentCopyMode _selectedCopyMode = RecruitmentCopyMode.Html;

    public string OrgName
    {
        get => _orgName;
        set { if (SetProperty(ref _orgName, value)) Rebuild(); }
    }

    public string Headline
    {
        get => _headline;
        set { if (SetProperty(ref _headline, value)) Rebuild(); }
    }

    public string BannerImagePath
    {
        get => _bannerImagePath;
        set { if (SetProperty(ref _bannerImagePath, value)) Rebuild(); }
    }

    public string OpeningStatement
    {
        get => _openingStatement;
        set { if (SetProperty(ref _openingStatement, value)) Rebuild(); }
    }

    public string WhyJoinText
    {
        get => _whyJoinText;
        set { if (SetProperty(ref _whyJoinText, value)) Rebuild(); }
    }

    public string CallToAction
    {
        get => _callToAction;
        set { if (SetProperty(ref _callToAction, value)) Rebuild(); }
    }

    public string WebsiteUrl
    {
        get => _websiteUrl;
        set { if (SetProperty(ref _websiteUrl, value)) Rebuild(); }
    }

    public string RsiUrl
    {
        get => _rsiUrl;
        set { if (SetProperty(ref _rsiUrl, value)) Rebuild(); }
    }

    public string DiscordUrl
    {
        get => _discordUrl;
        set { if (SetProperty(ref _discordUrl, value)) Rebuild(); }
    }

    public string Language
    {
        get => _language;
        set { if (SetProperty(ref _language, value)) Rebuild(); }
    }

    public string SignOff
    {
        get => _signOff;
        set { if (SetProperty(ref _signOff, value)) Rebuild(); }
    }

    public string GeneratedPost
    {
        get => _generatedPost;
        private set => SetProperty(ref _generatedPost, value);
    }

    public string HtmlPreview
    {
        get => _htmlPreview;
        private set => SetProperty(ref _htmlPreview, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool WebViewAvailable
    {
        get => _webViewAvailable;
        set => SetProperty(ref _webViewAvailable, value);
    }

    public bool ShowWhyJoin
    {
        get => _showWhyJoin;
        set { if (SetProperty(ref _showWhyJoin, value)) Rebuild(); }
    }

    public bool ShowDivisions
    {
        get => _showDivisions;
        set { if (SetProperty(ref _showDivisions, value)) Rebuild(); }
    }

    public RecruitmentCopyMode SelectedCopyMode
    {
        get => _selectedCopyMode;
        set { if (SetProperty(ref _selectedCopyMode, value)) Rebuild(); }
    }

    public string ValidationWarning
    {
        get => _validationWarning;
        private set
        {
            if (SetProperty(ref _validationWarning, value))
                OnPropertyChanged(nameof(HasValidationWarning));
        }
    }

    public bool HasValidationWarning => !string.IsNullOrEmpty(_validationWarning);
    public int CharacterCount => BuildPlainPost().Length;
    public bool IsOverLimit => CharacterCount > 2000;

    public ObservableCollection<RecruitmentCopyMode> CopyModes { get; } =
        new()
        {
            RecruitmentCopyMode.Html,
            RecruitmentCopyMode.Plain
        };

    public ObservableCollection<RecruitmentDivision> Divisions { get; } = new();

    public ICommand CopyCommand { get; }
    public ICommand ResetCommand { get; }

    public RecruitmentViewModel()
    {
        CopyCommand = new RelayCommand(CopyToClipboard);
        ResetCommand = new RelayCommand(ResetToDefaults);
        ResetToDefaults();
    }

    private void ResetToDefaults()
    {
        _orgName = "House Wolf";
        _headline = "STRENGTH IN UNITY. VICTORY IN BATTLE.";
        _bannerImagePath = "https://i.imgur.com/66hj66F.png";
        _openingStatement =
            "House Wolf is a tight-knit, active org built around teamwork, good people, and shared experiences. " +
            "Whether you're here for combat, industry, exploration, or just flying with a solid group; there’s a place for you in the pack..";
        _whyJoinText =
            "Relaxed but organized; play how you want\n" +
            "Active community across multiple playstyles\n";
        _callToAction =
            "Join House Wolf today.";
        _websiteUrl = "https://www.housewolf.co/";
        _rsiUrl = "https://robertsspaceindustries.com/orgs/CUTTERWOLF";
        _discordUrl = "https://discord.gg/housewolf";
        // _language = "English | All time zones welcome";
        // _signOff = "This is the way.";
        _showWhyJoin = true;
        _showDivisions = true;
        _selectedCopyMode = RecruitmentCopyMode.Html;

        OnPropertyChanged(nameof(OrgName));
        OnPropertyChanged(nameof(Headline));
        OnPropertyChanged(nameof(BannerImagePath));
        OnPropertyChanged(nameof(OpeningStatement));
        OnPropertyChanged(nameof(WhyJoinText));
        OnPropertyChanged(nameof(CallToAction));
        OnPropertyChanged(nameof(WebsiteUrl));
        OnPropertyChanged(nameof(RsiUrl));
        OnPropertyChanged(nameof(DiscordUrl));
        OnPropertyChanged(nameof(Language));
        OnPropertyChanged(nameof(SignOff));
        OnPropertyChanged(nameof(ShowWhyJoin));
        OnPropertyChanged(nameof(ShowDivisions));
        OnPropertyChanged(nameof(SelectedCopyMode));

        Divisions.Clear();

        RegisterDivision(new RecruitmentDivision
        {
            IsEnabled = true,
            Emoji = "⚔",
            Name = "TACOPS — AIR WING",
            Tagline = "“When the battle ignites, we lead the charge.”",
            ImagePath = "https://i.imgur.com/lD4P6Cv.png?s=32",
            Description = "TACOPS dominates the battlespace from atmosphere to the vacuum of space."
        });

        RegisterDivision(new RecruitmentDivision
        {
            IsEnabled = true,
            Emoji = "⚙",
            Name = "LOCOPS — THE BACKBONE",
            Tagline = "Wars are won by those who can sustain them.",
            ImagePath = "https://i.imgur.com/HfepOyk.png?s=32",
            Description = "LOCOPS secures the resources that fuel every campaign.."
        });

        RegisterDivision(new RecruitmentDivision
        {
            IsEnabled = true,
            Emoji = "🛡",
            Name = "SPECOPS — THE SHADOW",
            Tagline = "Silent when needed. Relentless when unleashed",
            ImagePath = "https://i.imgur.com/6rilziI.png?s=32",
            Description = "SPECOPS moves unseen and leaves no margin for failure.."
        });

        RegisterDivision(new RecruitmentDivision
        {
            IsEnabled = true,
            Emoji = "🧭",
            Name = "ARCOPS — THE VANGUARD",
            Tagline = "We map the unknown so others may conquer it.",
            ImagePath = "https://i.imgur.com/TlCNBwW.png?s=32",
            Description = "ARCOPS transforms discovery into capability, providing the knowledge, technology, and equipment that empower every other command to succeed."
        });

        Rebuild();
        StatusMessage = string.Empty;
    }

    private void RegisterDivision(RecruitmentDivision division)
    {
        division.PropertyChanged += (_, _) => Rebuild();
        Divisions.Add(division);
    }

    private void CopyToClipboard()
    {
        if (IsOverLimit)
        {
            StatusMessage = $"Post is {CharacterCount - 2000} characters over the 2000 limit — trim content first.";
            return;
        }

        if (SelectedCopyMode == RecruitmentCopyMode.Html)
        {
            var htmlFragment = BuildRichTextHtmlFragment();
            var dataObject = new DataObject();

            // Keep the clipboard HTML-only so Spectrum can paste the rendered markup
            // instead of downgrading to plain text URLs.
            dataObject.SetData(DataFormats.Html, CreateHtmlClipboardData(htmlFragment));

            Clipboard.SetDataObject(dataObject, true);
        }
        else
        {
            Clipboard.SetText(GeneratedPost);
        }

        StatusMessage = SelectedCopyMode switch
        {
            RecruitmentCopyMode.Plain => "Plain recruitment post copied.",
            RecruitmentCopyMode.Html => "RSI rich text copied.",
            _ => "Recruitment post copied."
        };
    }

    private void Rebuild()
    {
        GeneratedPost = SelectedCopyMode switch
        {
            RecruitmentCopyMode.Plain => BuildPlainPost(),
            RecruitmentCopyMode.StyledBbCode => BuildSpectrumBbCodePost(),
            RecruitmentCopyMode.Html => BuildRichTextHtmlFragment(),
            _ => BuildSpectrumBbCodePost()
        };

        HtmlPreview = BuildHtmlFromRichTextFragment(BuildRichTextHtmlFragment());

        OnPropertyChanged(nameof(CharacterCount));
        OnPropertyChanged(nameof(IsOverLimit));

        ValidationWarning = ComputeValidationWarning();
    }

    private string BuildSpectrumBbCodePost()
    {
        var sb = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(_bannerImagePath))
        {
            sb.AppendLine($"[img]{ResolveImg(_bannerImagePath)}[/img]");
            sb.AppendLine();
        }

        sb.AppendLine($"[center][size=18][b]🐺 {_orgName.ToUpperInvariant()}[/b][/size][/center]");
        sb.AppendLine($"[center][i]{_headline}[/i][/center]");

        if (!string.IsNullOrWhiteSpace(_openingStatement))
        {
            sb.AppendLine(_openingStatement.Trim());
        }

        if (_showDivisions)
        {
            sb.AppendLine("[size=16][b]🐺 FIND YOUR PURPOSE. EARN YOUR PLACE.[/b][/size]");

            foreach (var division in Divisions.Where(d => d.IsEnabled))
            {
                sb.AppendLine($"[img]{ResolveImg(division.ImagePath)}[/img] [b]{division.Name}[/b]");

                if (!string.IsNullOrWhiteSpace(division.Tagline))
                    sb.AppendLine($"[i]{division.Tagline}[/i]");

                if (!string.IsNullOrWhiteSpace(division.Description))
                    sb.AppendLine(division.Description.Trim());

                sb.AppendLine();
            }
        }

        if (_showWhyJoin)
        {
            sb.AppendLine("[size=16][b]🔥 WHY JOIN HOUSE WOLF[/b][/size]");

            foreach (var line in _whyJoinText.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                sb.AppendLine($"• {line.Trim()}");

   
        }

        // sb.AppendLine("[size=16][b]📡 THE CALL HAS BEEN SOUNDED[/b][/size]");
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(_callToAction))
        {
            sb.AppendLine(_callToAction.Trim());
        }

        AppendBbCodeLink(sb, "House Wolf Home", _websiteUrl);
        AppendBbCodeLink(sb, "RSI Org Page", _rsiUrl);
        AppendBbCodeLink(sb, "Discord", _discordUrl);

        if (!string.IsNullOrWhiteSpace(_language))
            sb.AppendLine($"• Language: {_language}");
        

        if (!string.IsNullOrWhiteSpace(_signOff))
            sb.AppendLine($"[i]{_signOff}[/i]");

        return sb.ToString();
    }

    private string BuildPlainPost()
    {
        var sb = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(_bannerImagePath))
        {
            sb.AppendLine(ResolveImg(_bannerImagePath));
        }

        sb.AppendLine($"🐺 {_orgName.ToUpperInvariant()}");

        if (!string.IsNullOrWhiteSpace(_openingStatement))
        {
            sb.AppendLine(_openingStatement.Trim());
        }

        if (_showDivisions)
        {
            foreach (var division in Divisions.Where(d => d.IsEnabled))
            {
                if (!string.IsNullOrWhiteSpace(division.ImagePath))
                    sb.AppendLine(ResolveImg(division.ImagePath));

                sb.AppendLine(division.Name);

                if (!string.IsNullOrWhiteSpace(division.Tagline))
                    sb.AppendLine(division.Tagline);

                if (!string.IsNullOrWhiteSpace(division.Description))
                    sb.AppendLine(division.Description);

                sb.AppendLine();
            }
        }

        if (_showWhyJoin)
        {
            sb.AppendLine("🔥 WHY JOIN HOUSE WOLF");

            foreach (var line in _whyJoinText.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                sb.AppendLine($"• {line.Trim()}");
        }

        AppendTextLink(sb, "House Wolf Home", _websiteUrl);
        AppendTextLink(sb, "RSI Org Page", _rsiUrl);
        AppendTextLink(sb, "Discord", _discordUrl);
        return sb.ToString();
    }

    private string BuildHtmlFromRichTextFragment(string fragment)
    {
        return HtmlShell
            .Replace("{{THREAD_TITLE}}", Enc($"[HW] {_orgName} wants YOU! (English)"), StringComparison.Ordinal)
            .Replace("{{POST_BODY}}", fragment, StringComparison.Ordinal);
    }

    private string BuildRichTextHtmlFragment()
    {
        var body = new StringBuilder();

        body.Append("<div class='post-text'>");

        if (!string.IsNullOrWhiteSpace(_bannerImagePath))
        {
            body.Append($"<img class='banner-img' src='{Enc(ResolveImg(_bannerImagePath))}' alt='Recruitment Image'>");
        }

        body.Append($"<div class='center'><span class='section-title'>🐺 {Enc(_orgName.ToUpperInvariant())}</span></div>");
        body.Append($"<div class='center'><em>{Enc(_headline)}</em></div>");

        if (!string.IsNullOrWhiteSpace(_openingStatement))
        {
            body.Append($"<p>{Enc(_openingStatement.Trim())}</p>");
        }

        if (_showDivisions)
        {
            body.Append($"<div class='section-title'>🐺 FIND YOUR PURPOSE. EARN YOUR PLACE.</div>");

            foreach (var division in Divisions.Where(d => d.IsEnabled))
            {
                body.Append("<div class='division-block'>");
                body.Append("<div class='division-line'>");

                if (!string.IsNullOrWhiteSpace(division.ImagePath))
                    body.Append($"<img class='division-icon' src='{Enc(ResolveImg(division.ImagePath))}' alt='Division icon'>");

                body.Append($"<span class='division-title'>{Enc(division.Name)}</span>");
                body.Append("</div>");

                if (!string.IsNullOrWhiteSpace(division.Tagline))
                    body.Append($"<div class='division-tagline'><em>{Enc(division.Tagline)}</em></div>");

                if (!string.IsNullOrWhiteSpace(division.Description))
                    body.Append($"<div class='division-description'>{Enc(division.Description.Trim())}</div>");

                body.Append("</div>");
            }
        }

        if (_showWhyJoin)
        {
            body.Append($"<div class='section-title'>🔥 WHY JOIN HOUSE WOLF</div>");
            body.Append("<ul class='why-join-list'>");

            foreach (var line in _whyJoinText.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                body.Append($"<li>{Enc(line.Trim())}</li>");

            body.Append("</ul>");
        }

        if (!string.IsNullOrWhiteSpace(_callToAction))
        {
            body.Append($"<p>{Enc(_callToAction.Trim())}</p>");
        }

        AppendHtmlLink(body, _websiteUrl, "House Wolf Home");
        AppendHtmlLink(body, _rsiUrl, "RSI Org Page");
        AppendHtmlLink(body, _discordUrl, "Discord");

        if (!string.IsNullOrWhiteSpace(_language))
            body.Append($"<p>• {Enc(_language)}</p>");

        if (!string.IsNullOrWhiteSpace(_signOff))
            body.Append($"<div class='center'><em>{Enc(_signOff)}</em></div>");

        body.Append("</div>");
        return body.ToString();
    }

    private static void AppendHtmlLink(StringBuilder body, string url, string label)
    {
        if (string.IsNullOrWhiteSpace(url))
            return;

        var encodedUrl = Enc(url);
        var encodedLabel = Enc(label);
        body.Append($"<p>• <a href='{encodedUrl}'>{encodedLabel}</a></p>");
    }

    private string ComputeValidationWarning()
    {
        var parts = new List<string>();

        if (string.IsNullOrWhiteSpace(_orgName))
            parts.Add("Org name is empty");

        if (string.IsNullOrWhiteSpace(_openingStatement))
            parts.Add("Opening statement is empty");

        if (_showDivisions && Divisions.All(d => !d.IsEnabled))
            parts.Add("No divisions are enabled");

        return string.Join("  ·  ", parts);
    }

    private static void AppendTextLink(StringBuilder sb, string label, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        sb.AppendLine(string.Create(CultureInfo.InvariantCulture, $"• {label}: {value}"));
    }

    private static void AppendBbCodeLink(StringBuilder sb, string label, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        if (value.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            sb.AppendLine(string.Create(CultureInfo.InvariantCulture, $"• [url={value}]{label}[/url]"));
        else
            sb.AppendLine(string.Create(CultureInfo.InvariantCulture, $"• {label}: {value}"));
    }

    private static string ResolveImg(string path) =>
        path.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? path
            : $"https://assets.packtracker.local/{path}";

    private static string Enc(string value) => WebUtility.HtmlEncode(value);

    private static string CreateHtmlClipboardData(string htmlFragment)
    {
        const string fragmentStart = "<!--StartFragment-->";
        const string fragmentEnd = "<!--EndFragment-->";
        var headerTemplate = "Version:0.9\r\nStartHTML:{0:00000000}\r\nEndHTML:{1:00000000}\r\nStartFragment:{2:00000000}\r\nEndFragment:{3:00000000}\r\n";
        var body = $"<html><body>{fragmentStart}{htmlFragment}{fragmentEnd}</body></html>";
        var placeholderHeader = string.Format(CultureInfo.InvariantCulture, headerTemplate, 0, 0, 0, 0);
        var startHtml = Encoding.UTF8.GetByteCount(placeholderHeader);
        var startFragment = startHtml + Encoding.UTF8.GetByteCount("<html><body><!--StartFragment-->");
        var endFragment = startHtml + Encoding.UTF8.GetByteCount($"<html><body><!--StartFragment-->{htmlFragment}");
        var endHtml = startHtml + Encoding.UTF8.GetByteCount(body);

        return string.Format(
            CultureInfo.InvariantCulture,
            headerTemplate,
            startHtml,
            endHtml,
            startFragment,
            endFragment) + body;
    }

    private const string HtmlShell = """
        <!DOCTYPE html>
        <html lang="en">
        <head>
        <meta charset="UTF-8">
        <meta name="viewport" content="width=device-width, initial-scale=1.0">
        <style>
          *, *::before, *::after { box-sizing: border-box; }

          html, body {
            margin: 0;
            background: #0b0e13;
            color: #aeb7c2;
            font-family: "Segoe UI", Tahoma, Geneva, Verdana, sans-serif;
            font-size: 14px;
            line-height: 1.7;
          }

          body { min-height: 100vh; }

          .sp-nav {
            background: linear-gradient(180deg, #0e1218 0%, #0d1016 100%);
            border-bottom: 1px solid #1a2030;
            padding: 14px 22px;
            display: flex;
            align-items: center;
            flex-wrap: wrap;
            gap: 8px;
            color: #5d6878;
            font-size: 12px;
          }

          .sp-logo {
            color: #c7d0db;
            font-weight: 800;
            letter-spacing: .18em;
            margin-right: 10px;
          }

          .sp-nav-sep { color: #2f3847; }

          .sp-nav-crumb.active { color: #8c98a6; }

          .sp-thread-header {
            background: linear-gradient(180deg, #10141b 0%, #0e1218 100%);
            border-bottom: 1px solid #1a2030;
            padding: 18px 24px 16px;
          }

          .sp-thread-title {
            color: #d6dde6;
            font-size: 22px;
            font-weight: 700;
            margin-bottom: 8px;
          }

          .sp-thread-meta {
            display: flex;
            align-items: center;
            flex-wrap: wrap;
            gap: 10px;
            color: #667283;
            font-size: 12px;
          }

          .sp-tag {
            padding: 2px 8px;
            border-radius: 3px;
            border: 1px solid #233044;
            background: #151c28;
            color: #8694a4;
            font-weight: 600;
            letter-spacing: .04em;
          }

          .sp-page {
            max-width: 1040px;
            margin: 0 auto;
            padding: 22px 22px 48px;
          }

          .sp-post {
            border: 1px solid #1a2130;
            background: #121722;
            border-radius: 4px;
            overflow: hidden;
            box-shadow: 0 14px 32px rgba(0,0,0,.25);
          }

          .sp-post-head {
            display: flex;
            align-items: center;
            gap: 12px;
            padding: 14px 18px;
            background: #0f141d;
            border-bottom: 1px solid #1a2130;
          }

          .sp-avatar {
            width: 36px;
            height: 36px;
            border-radius: 50%;
            border: 1px solid #2a3344;
            background: #1b2432;
            color: #d8dfe7;
            display: flex;
            align-items: center;
            justify-content: center;
            flex-shrink: 0;
            font-size: 17px;
          }

          .sp-author { flex: 1; min-width: 0; }

          .sp-author-name {
            color: #d4dbe4;
            font-size: 13px;
            font-weight: 700;
          }

          .sp-author-org,
          .sp-post-date {
            color: #687486;
            font-size: 11px;
          }

          .sp-post-body {
            padding: 22px;
            color: #a9b4bf;
          }

          .sp-post-body strong {
            color: #d6dde6;
            font-weight: 800;
          }

          .sp-post-body em {
            color: #909baa;
          }

          .sp-post-body a {
            color: #6ba8db;
            text-decoration: none;
          }

          .sp-post-body a:hover {
            text-decoration: underline;
          }

          .banner-img {
            width: 100%;
            max-height: 280px;
            object-fit: cover;
            border-radius: 4px;
            display: block;
            margin: 0 0 22px;
            border: 1px solid #1a2130;
          }

          .division-line {
            display: flex;
            align-items: center;
            gap: 10px;
            margin: 10px 0 4px;
          }

          .division-block {
            margin: 0 0 16px;
          }

          .division-icon {
            width: 28px;
            height: 28px;
            object-fit: cover;
            border-radius: 6px;
            border: 1px solid #1a2130;
            flex: 0 0 auto;
          }

          .division-title {
            color: #d6dde6;
            font-size: 16px;
            font-weight: 800;
            letter-spacing: .03em;
          }

          .division-tagline {
            color: #909baa;
            font-style: italic;
            margin: 0 0 4px 38px;
          }

          .division-description {
            margin-left: 38px;
          }

          .why-join-list {
            margin: 6px 0 0 22px;
            padding: 0;
          }

          .why-join-list li {
            margin-bottom: 2px;
          }

          .post-text {
            font-size: 14px;
            line-height: 1.75;
            word-break: break-word;
          }

          .center {
            text-align: center;
            margin: 4px 0;
          }

          .section-title {
            display: inline-block;
            color: #d6dde6;
            font-size: 17px;
            font-weight: 800;
            margin-top: 14px;
            margin-bottom: 4px;
            letter-spacing: .03em;
          }
        </style>
        </head>
        <body>
          <nav class="sp-nav">
            <span class="sp-logo">SPECTRUM</span>
            <span>SC Persistent Universe</span>
            <span class="sp-nav-sep">/</span>
            <span>Organizations</span>
            <span class="sp-nav-sep">/</span>
            <span>Recruitment</span>
            <span class="sp-nav-sep">/</span>
            <span class="sp-nav-crumb active">{{THREAD_TITLE}}</span>
          </nav>

          <div class="sp-thread-header">
            <div class="sp-thread-title">{{THREAD_TITLE}}</div>
            <div class="sp-thread-meta">
              <span class="sp-tag">DISCUSSION</span>
              <span class="sp-tag">NEED MEMBERS</span>
              <span>Organizations Recruitment · preview</span>
            </div>
          </div>

          <div class="sp-page">
            <div class="sp-post">
              <div class="sp-post-head">
                <div class="sp-avatar">🐺</div>
                <div class="sp-author">
                  <div class="sp-author-name">Preview</div>
                  <div class="sp-author-org">House Wolf · Member</div>
                </div>
                <div class="sp-post-date">Preview mode</div>
              </div>

              <div class="sp-post-body">
                {{POST_BODY}}
              </div>
            </div>
          </div>
        </body>
        </html>
        """;
}

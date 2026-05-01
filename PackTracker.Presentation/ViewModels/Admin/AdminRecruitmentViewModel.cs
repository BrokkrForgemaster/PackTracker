using System.Collections.ObjectModel;
using System.Globalization;
using System.Net;
using System.Text;
using System.Windows;
using System.Windows.Input;
using PackTracker.Presentation.Commands;

namespace PackTracker.Presentation.ViewModels.Admin;

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

public sealed class AdminRecruitmentViewModel : ViewModelBase
{
    private static readonly string[] ParagraphSeparators = ["\n\n"];

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

    public int CharacterCount => _generatedPost.Length;
    public bool IsOverLimit => _generatedPost.Length > 2000;

    public ObservableCollection<RecruitmentDivision> Divisions { get; } = new();

    public ICommand CopyCommand { get; }
    public ICommand ResetCommand { get; }

    public AdminRecruitmentViewModel()
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
            "House Wolf is a disciplined, active org with structure, purpose, and teeth. " +
            "Whether you fly combat, run logistics, or operate in the shadows — there's a place for you in the pack.";
        _whyJoinText =
            "Rank with clear progression\n" +
            "Structured PvP & PvE roles\n" +
            "Strong logistics and support backbone\n" +
            "Coordinated missions & fleet operations\n" +
            "A united, disciplined, battle-ready pack";
        _callToAction =
            "Do you fight alone — or run with the pack?\n\n" +
            "Join House Wolf today.";
        _websiteUrl = "https://www.housewolf.co/";
        _rsiUrl = "https://robertsspaceindustries.com/orgs/CUTTERWOLF";
        _discordUrl = "https://discord.gg/housewolf";
        _language = "English | All time zones welcome";
        _signOff = "This is the way.";

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

        Divisions.Clear();

        RegisterDivision(new RecruitmentDivision
        {
            IsEnabled = true,
            Emoji = "⚔",
            Name = "TACOPS — THE SPEAR",
            Tagline = "First in. Last standing.",
            ImagePath = "https://i.imgur.com/lD4P6Cv.png",
            Description =
                "Elite warriors specializing in PvP & PvE. Strike fast. Dominate every engagement."
        });

        RegisterDivision(new RecruitmentDivision
        {
            IsEnabled = true,
            Emoji = "⚙",
            Name = "LOCOPS — THE BACKBONE",
            Tagline = "Without us, nothing moves.",
            ImagePath = "https://i.imgur.com/HfepOyk.png",
            Description =
                "The engine of House Wolf. Mining, salvage, transport, support, and engineering — you power our war machine."
        });

        RegisterDivision(new RecruitmentDivision
        {
            IsEnabled = true,
            Emoji = "🛡",
            Name = "SPECOPS — THE SHADOW",
            Tagline = "You won't see us coming. Neither will they.",
            ImagePath = "https://i.imgur.com/6rilziI.png",
            Description =
                "Black ops specialists running covert strikes, infiltration, rapid response, and mission-critical support."
        });

        RegisterDivision(new RecruitmentDivision
        {
            IsEnabled = true,
            Emoji = "🧭",
            Name = "ARCOPS — THE VANGUARD",
            Tagline = "We go first. So the pack can go further.",
            ImagePath = "https://i.imgur.com/TlCNBwW.png",
            Description =
                "Explorers and crafters. We map the unknown, gather intel, craft equipment, and build the foundation every other division stands on."
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
            StatusMessage = $"Post is {_generatedPost.Length - 2000} characters over the 2000 limit — trim content first.";
            return;
        }
        Clipboard.SetText(GeneratedPost);
        StatusMessage = "Copied to clipboard.";
    }

    private void Rebuild()
    {
        GeneratedPost = BuildPost();
        OnPropertyChanged(nameof(CharacterCount));
        OnPropertyChanged(nameof(IsOverLimit));
        HtmlPreview = BuildHtml();
    }

    private string BuildPost()
    {
        var sb = new StringBuilder();
        const string bar = "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━";
        const string topEdge = "▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀";
        const string botEdge = "▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄";

        var spacedName = string.Join(" ", _orgName.ToUpperInvariant().ToCharArray());

        sb.AppendLine(topEdge);
        sb.AppendLine(string.Create(CultureInfo.InvariantCulture, $"              {spacedName}"));
        sb.AppendLine(botEdge);
        sb.AppendLine(string.Create(CultureInfo.InvariantCulture, $"         {_headline}"));
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine(_openingStatement);
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine(bar);
        sb.AppendLine("              FIND YOUR PURPOSE. EARN YOUR PLACE.");
        sb.AppendLine(bar);
        sb.AppendLine();

        foreach (var division in Divisions.Where(d => d.IsEnabled))
        {
            sb.AppendLine(string.Create(CultureInfo.InvariantCulture, $"**{division.Emoji} {division.Name}**"));
            sb.AppendLine(string.Create(CultureInfo.InvariantCulture, $"*{division.Tagline}*"));
            sb.AppendLine();
            sb.AppendLine(division.Description);
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
        }

        sb.AppendLine(bar);
        sb.AppendLine("                    WHY HOUSE WOLF");
        sb.AppendLine(bar);
        sb.AppendLine();

        foreach (var line in _whyJoinText.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            sb.AppendLine(string.Create(CultureInfo.InvariantCulture, $"✦ {line.Trim()}"));
            sb.AppendLine();
        }

        sb.AppendLine(bar);
        sb.AppendLine("                THE CALL HAS BEEN SOUNDED");
        sb.AppendLine(bar);
        sb.AppendLine();
        sb.AppendLine(_callToAction);
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        AppendTextLink(sb, "House Wolf Home", _websiteUrl);
        AppendTextLink(sb, "RSI Org Page", _rsiUrl);
        AppendTextLink(sb, "Discord", _discordUrl);
        AppendTextLink(sb, "Language", _language);
        sb.AppendLine();
        sb.AppendLine(string.Create(CultureInfo.InvariantCulture, $"*{_signOff}*"));

        return sb.ToString();
    }

    private string BuildHtml()
    {
        var body = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(_bannerImagePath))
            body.AppendLine(string.Create(CultureInfo.InvariantCulture, $"<img class='banner-img' src='{ResolveImg(_bannerImagePath)}' alt='Banner'>"));

        var spacedName = string.Join("&nbsp;", _orgName.ToUpperInvariant().ToCharArray());
        body.AppendLine(string.Create(CultureInfo.InvariantCulture, $"""
            <div class="org-block">
              <div class="post-org-name">{spacedName}</div>
              <div class="post-org-sub">{Enc(_headline)}</div>
            </div>
            <hr class="sp-hr">
            """));

        body.Append(TextToHtml(_openingStatement));
        body.AppendLine("<hr class='sp-hr'>");
        body.AppendLine("<p class='sec-head'>FIND YOUR PURPOSE. EARN YOUR PLACE.</p>");

        foreach (var division in Divisions.Where(d => d.IsEnabled))
        {
            body.AppendLine(BuildDivisionHtml(division));
        }

        body.AppendLine("<p class='sec-head'>Why Join House Wolf?</p>");
        body.AppendLine("<div class='why-list'>");
        foreach (var line in _whyJoinText.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
                continue;

            var dashIndex = trimmed.IndexOf(" — ", StringComparison.Ordinal);
            var itemHtml = dashIndex >= 0
                ? $"<strong>{Enc(trimmed[..dashIndex])}</strong> — {Enc(trimmed[(dashIndex + 3)..])}"
                : Enc(trimmed);
            body.AppendLine(string.Create(CultureInfo.InvariantCulture, $"<div class='why-line'><span class='why-bullet'>✦</span><span>{itemHtml}</span></div>"));
        }
        body.AppendLine("</div>");
        body.AppendLine("<hr class='sp-hr'>");

        body.Append(TextToHtml(_callToAction));
        body.AppendLine("<hr class='sp-hr'>");

        body.AppendLine("<div class='post-links'>");
        AppendHtmlLink(body, "House Wolf Home", _websiteUrl);
        AppendHtmlLink(body, "RSI Org Page", _rsiUrl);
        AppendHtmlLink(body, "Discord", _discordUrl);
        AppendHtmlLink(body, "Language", _language);
        body.AppendLine("</div>");

        body.AppendLine(string.Create(CultureInfo.InvariantCulture, $"<p class='post-signoff'>{Enc(_signOff)}</p>"));

        return HtmlShell
            .Replace("{{THREAD_TITLE}}", Enc($"[HW] {_orgName} wants YOU! (English)"), StringComparison.Ordinal)
            .Replace("{{POST_BODY}}", body.ToString(), StringComparison.Ordinal);
    }

    private static void AppendTextLink(StringBuilder sb, string label, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        sb.AppendLine(string.Create(CultureInfo.InvariantCulture, $"• {label}: {value}"));
    }

    private static void AppendHtmlLink(StringBuilder sb, string label, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        sb.AppendLine(string.Create(CultureInfo.InvariantCulture, $"""
            <div class="link-row">
              <span class="link-label">{Enc(label)}:</span>
              <span class="link-value">{FormatLinkValue(value)}</span>
            </div>
            """));
    }

    private static string BuildDivisionHtml(RecruitmentDivision division)
    {
        var nameIcon = string.IsNullOrWhiteSpace(division.ImagePath)
            ? $"{Enc(division.Emoji)} "
            : $"<img class='div-icon' src='{ResolveImg(division.ImagePath)}' alt='{Enc(division.Emoji)}'> ";

        var description = Enc(division.Description).Replace("\n", "<br>", StringComparison.Ordinal);

        return $"""
            <div class="division-entry">
              <p class="division-name"><strong>{nameIcon}{Enc(division.Name)}</strong></p>
              <p class="division-tagline"><em>{Enc(division.Tagline)}</em></p>
              <p class="division-description">{description}</p>
            </div>
            <hr class="sp-hr">
            """;
    }

    private static string FormatLinkValue(string value)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            var encoded = Enc(value);
            return $"<a href='{encoded}'>{encoded}</a>";
        }

        return Enc(value);
    }

    private static string ResolveImg(string path) =>
        path.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? path
            : $"https://assets.packtracker.local/{path}";

    private static string Enc(string value) => WebUtility.HtmlEncode(value);

    private static string TextToHtml(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var sb = new StringBuilder();
        foreach (var paragraph in text.Split(ParagraphSeparators, StringSplitOptions.RemoveEmptyEntries))
        {
            var inner = Enc(paragraph.Trim()).Replace("\n", "<br>", StringComparison.Ordinal);
            sb.Append("<p>").Append(inner).Append("</p>");
        }

        return sb.ToString();
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

          body {
            min-height: 100vh;
          }

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

          .sp-nav-sep {
            color: #2f3847;
          }

          .sp-nav-crumb.active {
            color: #8c98a6;
          }

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
            box-shadow: 0 14px 32px rgba(0, 0, 0, .25);
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

          .sp-author {
            flex: 1;
            min-width: 0;
          }

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

          .sp-post-body p {
            margin: 0 0 14px;
          }

          .sp-post-body strong {
            color: #d6dde6;
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
            max-height: 240px;
            object-fit: cover;
            border-radius: 3px;
            display: block;
            margin-bottom: 22px;
            border: 1px solid #1a2130;
          }

          .org-block {
            text-align: center;
            padding: 8px 0 20px;
          }

          .post-org-name {
            color: #e0e6ee;
            font-size: 24px;
            font-weight: 900;
            letter-spacing: .16em;
            margin-bottom: 6px;
          }

          .post-org-sub {
            color: #c9aa54;
            font-size: 11px;
            font-weight: 700;
            letter-spacing: .12em;
          }

          .sp-hr {
            border: 0;
            border-top: 1px solid #1b2433;
            margin: 18px 0;
          }

          .sec-head {
            color: #d6dde6;
            font-size: 15px;
            font-weight: 700;
            margin-bottom: 14px;
            letter-spacing: .01em;
          }

          .division-entry {
            margin-bottom: 4px;
          }

          .division-name {
            margin-bottom: 4px !important;
          }

          .division-tagline {
            margin-bottom: 10px !important;
          }

          .division-description {
            margin-bottom: 0 !important;
          }

          .div-icon {
            width: 22px;
            height: 22px;
            object-fit: cover;
            border-radius: 3px;
            vertical-align: middle;
            margin-bottom: 2px;
          }

          .why-list {
            display: grid;
            gap: 6px;
          }

          .why-line {
            display: grid;
            grid-template-columns: 18px 1fr;
            gap: 8px;
            padding: 3px 0;
            border-bottom: 1px solid rgba(39, 49, 68, .5);
          }

          .why-line:last-child {
            border-bottom: 0;
          }

          .why-bullet {
            color: #d0b061;
          }

          .post-links {
            display: grid;
            gap: 8px;
          }

          .link-row {
            display: flex;
            flex-wrap: wrap;
            gap: 8px;
          }

          .link-label {
            color: #d6dde6;
            font-weight: 700;
          }

          .post-signoff {
            margin-top: 18px !important;
            text-align: center;
            font-style: italic;
            color: #c9aa54;
          }

          @media (max-width: 700px) {
            .sp-page {
              padding: 14px 10px 28px;
            }

            .sp-thread-header,
            .sp-post-body,
            .sp-post-head {
              padding-left: 14px;
              padding-right: 14px;
            }

            .division-entry {
              grid-template-columns: 1fr;
            }

            .division-media {
              justify-content: flex-start;
            }
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
                  <div class="sp-author-name">Admin Preview</div>
                  <div class="sp-author-org">House Wolf · Hand of the Clan</div>
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

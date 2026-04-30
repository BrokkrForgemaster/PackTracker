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
        _bannerImagePath = "HousewolfBanner.png";
        _openingStatement =
            "Somewhere in the verse, a fight is already happening.\n\n" +
            "A mining convoy is being hit. A station is under siege. A contract just went loud. " +
            "And while lone wolves scatter and mercs count their credits before the smoke clears — " +
            "House Wolf answers the call together.\n\n" +
            "We are not a lobby. We are not a Discord server with a pretty tag. We are an organization " +
            "with structure, purpose, and teeth. Forged in battle, sustained by loyalty, and growing " +
            "stronger with every pilot who finds their place in the pack.\n\n" +
            "The verse is brutal. It rewards those who move with intention.\n\n" +
            "Move with us.";
        _whyJoinText =
            "Rank that means something — Progression is earned, not gifted. Climb through a structured hierarchy and lead when you're ready.\n" +
            "Operations with actual coordination — Scheduled ops, voice comms, real planning. Not just \"who wants to shoot things tonight.\"\n" +
            "Every role matters — Combat, industry, recon, exploration, crafting. The pack needs all of it. None of it is lesser.\n" +
            "Brotherhood that shows up — When your ship is on fire and your shields are gone, you want people on comms who already have a wing inbound. That's us.\n" +
            "A home that lasts — House Wolf is built for the long game. When Star Citizen hits its stride, we'll already be entrenched.";
        _callToAction =
            "The lone wolf doesn't survive the verse. The pack does.\n\n" +
            "If you're tired of pugging it alone, tired of orgs that ghost their own members, tired of " +
            "joining something that turns out to be nothing — come find out what it looks like when a " +
            "crew actually gives a damn.\n\n" +
            "Apply on RSI. Join the Discord. Tell us which division calls to you.\n\n" +
            "We'll take it from there.";
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
            ImagePath = "tacops.png",
            Description =
                "TACOPS is the tip of the blade. Fighter pilots, gunship crews, and battlefield commanders " +
                "who don't wait for permission to engage. Whether you're hunting bounties, running fleet PvP, " +
                "or holding a chokepoint under fire — if your instinct is to push forward, TACOPS is where " +
                "you belong. We don't ask if the odds are good. We make the odds irrelevant."
        });

        RegisterDivision(new RecruitmentDivision
        {
            IsEnabled = true,
            Emoji = "⚙",
            Name = "LOCOPS — THE BACKBONE",
            Tagline = "Without us, nothing moves.",
            ImagePath = "locops.png",
            Description =
                "Every warfighter needs fuel, ore, parts, and a ship that still flies. LOCOPS keeps the " +
                "machine running. Miners, haulers, salvagers, engineers — these are not support roles, they " +
                "are power projection roles. The org that controls resources controls the verse. LOCOPS " +
                "doesn't fight on the frontline because LOCOPS is the reason there's a frontline at all."
        });

        RegisterDivision(new RecruitmentDivision
        {
            IsEnabled = true,
            Emoji = "🛡",
            Name = "SPECOPS — THE SHADOW",
            Tagline = "You won't see us coming. Neither will they.",
            ImagePath = "specops.png",
            Description =
                "SPECOPS operates where the rules end. Covert interdictions, precision eliminations, " +
                "deep-recon insertion, high-value target extraction — if TACOPS is the punch, SPECOPS is " +
                "the knife already in the room before the fight starts. Entry is selective. Standards are " +
                "high. Results are absolute."
        });

        RegisterDivision(new RecruitmentDivision
        {
            IsEnabled = true,
            Emoji = "🧭",
            Name = "ARCOPS — THE VANGUARD",
            Tagline = "We go first. So the pack can go further.",
            ImagePath = "arcops.png",
            Description =
                "Before the battle, before the haul, before the strike — someone has to cross the threshold " +
                "into the unknown. ARCOPS is the forward edge of House Wolf, pushing into uncharted systems " +
                "to explore, analyze, and understand what lies beyond known space. Through scientific study, " +
                "data collection, and advanced crafting, ARCOPS doesn't just discover the verse — it " +
                "translates discovery into capability. The maps TACOPS uses, the technology SPECOPS deploys, " +
                "the equipment LOCOPS depends on — ARCOPS builds the foundation every other division stands on."
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
        Clipboard.SetText(GeneratedPost);
        StatusMessage = "Copied to clipboard.";
    }

    private void Rebuild()
    {
        GeneratedPost = BuildPost();
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
        var media = string.IsNullOrWhiteSpace(division.ImagePath)
            ? $"<div class='div-thumb div-thumb-fallback'>{Enc(division.Emoji)}</div>"
            : $"<img class='div-thumb' src='{ResolveImg(division.ImagePath)}' alt='{Enc(division.Name)}'>";

        var description = Enc(division.Description).Replace("\n", "<br>", StringComparison.Ordinal);

        return $"""
            <div class="division-entry">
              <div class="division-copy">
                <p class="division-name"><strong>{Enc(division.Emoji)} {Enc(division.Name)}</strong></p>
                <p class="division-tagline"><em>{Enc(division.Tagline)}</em></p>
                <p class="division-description">{description}</p>
              </div>
              <div class="division-media">{media}</div>
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
            display: grid;
            grid-template-columns: 1fr 140px;
            gap: 16px;
            align-items: start;
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

          .division-media {
            display: flex;
            justify-content: flex-end;
          }

          .div-thumb {
            width: 130px;
            height: 95px;
            object-fit: cover;
            border-radius: 3px;
            border: 1px solid #263044;
            background: #161d29;
            display: block;
          }

          .div-thumb-fallback {
            display: flex;
            align-items: center;
            justify-content: center;
            color: #d3dbe5;
            font-size: 38px;
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

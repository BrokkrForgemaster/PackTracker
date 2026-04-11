using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using PackTracker.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using PackTracker.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Caching.Memory;
using PackTracker.Infrastructure.Persistence;

using Microsoft.Extensions.Options;
using PackTracker.Application.Options;

namespace PackTracker.Api.Controllers;

/// <summary name="AuthController">
/// Handles Discord OAuth login and JWT/refresh token issuance.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
public class AuthController : ControllerBase
{
    private readonly ISettingsService _settingsService;
    private readonly JwtTokenService _jwt;
    private readonly IProfileService _profiles;
    private readonly AppDbContext _db;
    private readonly ILogger<AuthController> _logger;
    private readonly IMemoryCache _cache;
    private readonly AuthOptions _authOptions;

    public AuthController(
        ISettingsService settingsService,
        JwtTokenService jwt,
        IProfileService profiles,
        AppDbContext db,
        ILogger<AuthController> logger,
        IMemoryCache cache,
        IOptions<AuthOptions> authOptions)
    {
        _settingsService = settingsService;
        _jwt = jwt;
        _profiles = profiles;
        _db = db;
        _logger = logger;
        _cache = cache;
        _authOptions = authOptions.Value;
    }


    [AllowAnonymous]
    [HttpGet("login")]
    public async Task<IActionResult> Login([FromQuery] string? clientState,
        [FromServices] IAuthenticationSchemeProvider schemes)
    {
        _logger.LogInformation("Initiating Discord OAuth login redirect…");

        // Ensure the scheme is actually registered
        var scheme = await schemes.GetSchemeAsync("Discord");
        if (scheme is null)
            return HtmlMessage("❌ Discord auth scheme is not registered. Check startup configuration.");
        
        var clientId = _authOptions.Discord.ClientId;
        var clientSecret = _authOptions.Discord.ClientSecret;
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            return HtmlMessage("❌ Discord OAuth is not configured (missing ClientId/ClientSecret).");

        var redirectUri = "/api/v1/auth/complete";
        if (!string.IsNullOrWhiteSpace(clientState))
        {
            redirectUri = $"{redirectUri}?clientState={WebUtility.UrlEncode(clientState)}";
        }

        var props = new AuthenticationProperties
        {
            RedirectUri = redirectUri
        };

        if (!string.IsNullOrWhiteSpace(clientState))
            props.Items["state"] = clientState;

        return Challenge(props, "Discord"); // should 302 to Discord now
    }

 
    [Authorize(AuthenticationSchemes = "Cookies")]
    [HttpGet("complete")]
    public async Task<IActionResult> Complete(CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("👉 Entered /api/v1/auth/complete");

            if (!User.Identity?.IsAuthenticated ?? false)
            {
                _logger.LogWarning("⚠️ User not authenticated when reaching /complete.");
                return HtmlMessage("⚠️ Discord authentication failed (no identity).");
            }

            var discordId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var username = User.Identity?.Name ?? "(unknown)";
            var displayName = User.FindFirstValue("urn:discord:displayname");
            var avatarUrl = User.FindFirstValue("urn:discord:avatar:url");
            var discriminator = User.FindFirstValue("urn:discord:discriminator") ?? string.Empty;

            _logger.LogInformation("✅ Authenticated Discord user {User} ({Id})", username, discordId);

            var accessToken = await HttpContext.GetTokenAsync("access_token");
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                _logger.LogWarning("⚠️ Missing Discord access token in context.");
                return HtmlMessage("⚠️ Internal error: Missing Discord access token.");
            }

            // Use ProfileService to handle the heavy lifting (guild check, rank fetch, etc.)
            var profile = await _profiles.UpsertFromDiscordAsync(
                accessToken,
                discordId!,
                username,
                avatarUrl,
                ct);

            if (profile is null)
            {
                _logger.LogWarning("🚫 User {User} ({Id}) is not in the required Discord guild.", username, discordId);
                return HtmlMessage("🚫 Access Denied: You must be a member of the required Discord guild to use PackTracker.");
            }

            // Sync display name and discriminator if they changed
            profile.DiscordDisplayName = displayName ?? profile.DiscordDisplayName;
            profile.Discriminator = discriminator;
            await _db.SaveChangesAsync(ct);

            // Generate token pair
            var (access, refresh, expires) = await _jwt.IssueTokenPairAsync(profile, ct);

            // Store in memory cache for the client to poll
            var clientState = Request.Query.TryGetValue("clientState", out var stateVals)
                ? stateVals.ToString()
                : Request.Query.TryGetValue("state", out var legacyState)
                    ? legacyState.ToString()
                    : null;
            if (!string.IsNullOrWhiteSpace(clientState))
            {
                _cache.Set(GetCacheKey(clientState),
                    new LoginTokenPayload(access, refresh, expires),
                    TimeSpan.FromMinutes(5));
                _logger.LogInformation("Stored tokens for state {ClientState}", clientState);
            }
            else
            {
                _logger.LogWarning("OAuth completed without a clientState; WPF client cannot retrieve tokens.");
            }

            // Return HTML success screen
            return Content(SuccessHtml(), "text/html");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error during /api/v1/auth/complete processing.");
            return HtmlMessage($"❌ Error completing login:<br><code>{ex.Message}</code>");
        }
    }

    // =====================================================================
    // STEP 3: Poll from WPF app for token after login completes
    // =====================================================================
    [AllowAnonymous]
    [HttpGet("poll/{clientState}")]
    public IActionResult Poll(string clientState)
    {
        if (_cache.TryGetValue<LoginTokenPayload>(GetCacheKey(clientState), out var payload))
        {
            _cache.Remove(GetCacheKey(clientState));
            return Ok(payload);
        }

        return NotFound();
    }

    // =====================================================================
    // STEP 4: Refresh access tokens (optional)
    // =====================================================================
    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            return BadRequest(new { error = "Missing refresh token." });

        var user = await _jwt.ValidateRefreshTokenAsync(request.RefreshToken, ct);
        if (user == null)
        {
            _logger.LogWarning("Invalid refresh token attempt.");
            return Unauthorized(new { error = "Invalid or expired refresh token." });
        }

        var (access, refresh, expires) = await _jwt.IssueTokenPairAsync(user, ct);
        return Ok(new { access_token = access, refresh_token = refresh, expires_in = expires });
    }

    // =====================================================================
    // STEP 5: Logout (revoke refresh token)
    // =====================================================================
    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            return BadRequest(new { error = "Missing refresh token." });

        await _jwt.RevokeRefreshTokenAsync(request.RefreshToken, ct);
        _logger.LogInformation("User {UserId} logged out.", User.FindFirstValue(ClaimTypes.NameIdentifier));
        return Ok(new { message = "Logged out successfully." });
    }

    // =====================================================================
    // STEP 6: Current authenticated user info
    // =====================================================================
    [Authorize]
    [HttpGet("me")]
    public IActionResult Me()
    {
        var claims = User.Claims.Select(c => new { c.Type, c.Value });
        _logger.LogInformation("Fetched profile info for {Username}", User.Identity?.Name);
        return Ok(new { User.Identity?.Name, Claims = claims });
    }

    // =====================================================================
    // Helpers
    // =====================================================================
    private static string GetCacheKey(string state) => $"login-state:{state}";

    private ContentResult HtmlMessage(string message) =>
        Content($"""
                 <html>
                   <body style="background:#121212;color:#fff;font-family:sans-serif;text-align:center;padding-top:15%">
                     <h2>{message}</h2>
                     <button style="padding:10px 20px;border:none;border-radius:6px;background:#c2a23a;color:#000;font-weight:bold;margin-top:20px;" onclick="window.close()">Close Window</button>
                   </body>
                 </html>
                 """, "text/html");

    private static string SuccessHtml() => """
                                           <!DOCTYPE html>
                                           <html lang="en">
                                           <head>
                                             <meta charset="utf-8" />
                                             <meta name="viewport" content="width=device-width, initial-scale=1" />
                                             <title>PackTracker · Login Successful</title>
                                             <style>
                                               body {
                                                 background:#050505;
                                                 color:#f5f5f5;
                                                 font-family:'Segoe UI',sans-serif;
                                                 display:flex;
                                                 align-items:center;
                                                 justify-content:center;
                                                 height:100vh;
                                                 margin:0;
                                               }
                                               .card {
                                                 background:#161616;
                                                 border-radius:18px;
                                                 padding:34px;
                                                 box-shadow:0 12px 30px rgba(0,0,0,0.45);
                                                 text-align:center;
                                                 max-width:460px;
                                                 border:1px solid rgba(255,255,255,0.08);
                                               }
                                               .pill {
                                                 display:inline-block;
                                                 padding:8px 12px;
                                                 border-radius:999px;
                                                 background:rgba(194,162,58,0.12);
                                                 border:1px solid rgba(194,162,58,0.28);
                                                 color:#e5ca6f;
                                                 font-size:12px;
                                                 font-weight:600;
                                                 letter-spacing:0.04em;
                                                 margin-bottom:16px;
                                               }
                                               h1 {
                                                 margin:0 0 12px 0;
                                                 font-size:28px;
                                               }
                                               p {
                                                 color:#c8c8c8;
                                                 line-height:1.5;
                                                 margin:0;
                                               }
                                               .hint {
                                                 margin-top:16px;
                                                 font-size:13px;
                                                 color:#9e9e9e;
                                               }
                                               button {
                                                 margin-top:20px;
                                                 padding:12px 20px;
                                                 border:none;
                                                 border-radius:8px;
                                                 cursor:pointer;
                                                 background:#c2a23a;
                                                 color:#111;
                                                 font-weight:700;
                                               }
                                             </style>
                                             <script>
                                               (function closeSoon(){
                                                 function attemptClose() {
                                                   try {
                                                     window.opener = null;
                                                     window.open('', '_self');
                                                     window.close();
                                                   } catch { /* browser may block close */ }
                                                 }

                                                 attemptClose();
                                                 setTimeout(attemptClose, 900);
                                               })();
                                             </script>
                                           </head>
                                           <body>
                                             <div class="card">
                                               <div class="pill">HOUSE WOLF // AUTHORIZED</div>
                                               <h1>Discord authentication complete</h1>
                                               <p>PackTracker has received your login. You can return to the desktop app now.</p>
                                               <p class="hint">This tab will try to close automatically. If your browser blocks it, you can close this tab manually.</p>
                                               <button onclick="window.close()">Close Tab</button>
                                             </div>
                                           </body>
                                           </html>
                                           """;

    // Records for DTOs
    public record RefreshTokenRequest(string RefreshToken);

    public record LoginTokenPayload(string access_token, string refresh_token, int expires_in);
}

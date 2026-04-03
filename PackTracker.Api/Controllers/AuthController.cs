using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Caching.Memory;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Entities;
using PackTracker.Infrastructure.Security;
using PackTracker.Infrastructure.Persistence;

namespace PackTracker.Api.Controllers;

/// <summary>
/// Handles Discord OAuth login and JWT/refresh token issuance.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
public class AuthController : ControllerBase
{
    private readonly ISettingsService _settingsService;
    private readonly JwtTokenService _jwt;
    private readonly AppDbContext _db;
    private readonly ILogger<AuthController> _logger;
    private readonly IMemoryCache _cache;

    public AuthController(ISettingsService settingsService, JwtTokenService jwt, AppDbContext db, ILogger<AuthController> logger,
        IMemoryCache cache)
    {
        _settingsService = settingsService;
        _jwt = jwt;
        _db = db;
        _logger = logger;
        _cache = cache;
    }

    // =====================================================================
    // STEP 1: Initiate Discord OAuth login
    // =====================================================================
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

        // Ensure credentials exist (defensive; startup already fails fast)
        var settings = _settingsService.GetSettings();
        var clientId = settings.DiscordClientId;
        var clientSecret = settings.DiscordClientSecret;
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


    // =====================================================================
    // STEP 2: Discord redirects here after successful login
    // =====================================================================
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

            _logger.LogInformation("✅ Authenticated Discord user {User} ({Id})", username, discordId);

            var profile = await _db.Profiles.FirstOrDefaultAsync(p => p.DiscordId == discordId, ct);
            var avatarUrl = User.FindFirstValue("urn:discord:avatar:url");
            var discriminator = User.FindFirstValue("urn:discord:discriminator") ?? string.Empty;

            if (profile is null)
            {
                profile = new Profile
                {
                    Id = Guid.NewGuid(),
                    DiscordId = discordId!,
                    Username = username ?? "(unknown)",
                    Discriminator = discriminator,
                    AvatarUrl = avatarUrl ?? string.Empty,
                    CreatedAt = DateTime.UtcNow,
                    LastLogin = DateTime.UtcNow
                };
                _db.Profiles.Add(profile);
                _logger.LogInformation("Created profile for DiscordId={DiscordId}", discordId);
            }
            else
            {
                profile.Username = username ?? profile.Username;
                profile.Discriminator = discriminator;
                profile.AvatarUrl = avatarUrl ?? profile.AvatarUrl;
                profile.LastLogin = DateTime.UtcNow;
                _logger.LogInformation("Updated profile for DiscordId={DiscordId}", discordId);
            }

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
                                             <title>PackTracker · Login Successful</title>
                                             <style>
                                               body { background:#050505; color:#f5f5f5; font-family:'Segoe UI',sans-serif;
                                                      display:flex; align-items:center; justify-content:center;
                                                      height:100vh; margin:0; }
                                               .card { background:#161616; border-radius:16px; padding:32px;
                                                       box-shadow:0 12px 30px rgba(0,0,0,0.45); text-align:center;
                                                       max-width:420px; }
                                               button { margin-top:18px; padding:12px 20px; border:none;
                                                        border-radius:8px; cursor:pointer; background:#c2a23a;
                                                        font-weight:bold; }
                                             </style>
                                             <script>
                                               (function closeSoon(){
                                                 try {
                                                   window.opener = null;
                                                   window.open('', '_self');
                                                   window.close();
                                                 } catch { /* browsers sometimes block the close */ }
                                                 setTimeout(() => window.close(), 800);
                                               })();
                                             </script>
                                           </head>
                                           <body>
                                             <div class="card">
                                               <h1>✅ Discord Authentication Complete</h1>
                                               <p>You can return to PackTracker — the app is moving to your dashboard.</p>
                                               <button onclick="window.close()">Close Window</button>
                                             </div>
                                           </body>
                                           </html>
                                           """;

    // Records for DTOs
    public record RefreshTokenRequest(string RefreshToken);

    public record LoginTokenPayload(string access_token, string refresh_token, int expires_in);
}

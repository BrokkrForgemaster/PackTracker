using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using PackTracker.Application.Interfaces;
using PackTracker.Application.Options;

namespace PackTracker.Api.Controllers;

/// <summary>
/// Handles Discord OAuth login and JWT/refresh token issuance.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthWorkflowService _authWorkflow;
    private readonly ILogger<AuthController> _logger;
    private readonly AuthOptions _authOptions;

    public AuthController(
        IAuthWorkflowService authWorkflow,
        ILogger<AuthController> logger,
        IOptions<AuthOptions> authOptions)
    {
        ArgumentNullException.ThrowIfNull(authWorkflow);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(authOptions);

        _authWorkflow = authWorkflow;
        _logger = logger;
        _authOptions = authOptions.Value;
    }

    [AllowAnonymous]
    [HttpGet("login")]
    public async Task<IActionResult> Login(
        [FromQuery] string? clientState,
        [FromServices] IAuthenticationSchemeProvider schemes)
    {
        ArgumentNullException.ThrowIfNull(schemes);
        _logger.LogInformation("Initiating Discord OAuth login redirect.");

        var scheme = await schemes.GetSchemeAsync("Discord");
        if (scheme is null)
        {
            return HtmlMessage("Discord auth scheme is not registered. Check startup configuration.");
        }

        var clientId = _authOptions.Discord.ClientId;
        var clientSecret = _authOptions.Discord.ClientSecret;
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            return HtmlMessage("Discord OAuth is not configured (missing ClientId/ClientSecret).");
        }

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
        {
            props.Items["state"] = clientState;
        }

        return Challenge(props, "Discord");
    }

    [Authorize(AuthenticationSchemes = "Cookies")]
    [HttpGet("complete")]
    public async Task<IActionResult> Complete(CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("Entered /api/v1/auth/complete");

            if (!User.Identity?.IsAuthenticated ?? false)
            {
                _logger.LogWarning("User not authenticated when reaching /complete.");
                return HtmlMessage("Discord authentication failed (no identity).");
            }

            var discordId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var username = User.Identity?.Name ?? "(unknown)";
            var displayName = User.FindFirstValue("urn:discord:displayname");
            var avatarUrl = User.FindFirstValue("urn:discord:avatar:url");
            var discriminator = User.FindFirstValue("urn:discord:discriminator") ?? string.Empty;

            _logger.LogInformation("Authenticated Discord user {User} ({DiscordId})", username, discordId);

            var accessToken = await HttpContext.GetTokenAsync("access_token");
            if (string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(discordId))
            {
                _logger.LogWarning("Missing Discord access token or Discord identifier in context.");
                return HtmlMessage("Internal error: Missing Discord access token.");
            }

            var clientState = Request.Query.TryGetValue("clientState", out var stateVals)
                ? stateVals.ToString()
                : Request.Query.TryGetValue("state", out var legacyState)
                    ? legacyState.ToString()
                    : null;

            var result = await _authWorkflow.CompleteAsync(
                new AuthCompletionRequest(
                    discordId,
                    username,
                    displayName,
                    avatarUrl,
                    discriminator,
                    accessToken,
                    clientState),
                ct);

            if (result.Status == AuthCompletionStatus.AccessDenied)
            {
                return HtmlMessage(result.Message);
            }

            return Content(SuccessHtml(), "text/html");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during /api/v1/auth/complete processing.");
            return HtmlMessage($"Error completing login:<br><code>{WebUtility.HtmlEncode(ex.Message)}</code>");
        }
    }

    [AllowAnonymous]
    [HttpGet("poll/{clientState}")]
    public async Task<IActionResult> Poll(string clientState, CancellationToken ct)
    {
        var payload = await _authWorkflow.PollAsync(clientState, ct);
        if (payload is null)
        {
            return NotFound();
        }

        return Ok(new LoginTokenPayload(payload.AccessToken, payload.RefreshToken, payload.ExpiresIn));
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return BadRequest(new { error = "Missing refresh token." });
        }

        var result = await _authWorkflow.RefreshAsync(request.RefreshToken, ct);
        if (!result.Succeeded)
        {
            return Unauthorized(new { error = result.Message });
        }

        return Ok(new
        {
            access_token = result.AccessToken,
            refresh_token = result.RefreshToken,
            expires_in = result.ExpiresIn
        });
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return BadRequest(new { error = "Missing refresh token." });
        }

        await _authWorkflow.LogoutAsync(request.RefreshToken, ct);
        _logger.LogInformation("User {UserId} logged out.", User.FindFirstValue(ClaimTypes.NameIdentifier));
        return Ok(new { message = "Logged out successfully." });
    }

    [Authorize]
    [HttpGet("me")]
    public IActionResult Me()
    {
        var claims = User.Claims.Select(c => new { c.Type, c.Value });
        _logger.LogInformation("Fetched profile info for {Username}", User.Identity?.Name);
        return Ok(new { User.Identity?.Name, Claims = claims });
    }

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
                                                 background: #050505;
                                                 color: #f5f5f5;
                                                 font-family: 'Segoe UI', sans-serif;
                                                 display: flex;
                                                 align-items: center;
                                                 justify-content: center;
                                                 height: 100vh;
                                                 margin: 0;
                                               }

                                               .card {
                                                 background: #161616;
                                                 border-radius: 18px;
                                                 padding: 34px;
                                                 box-shadow: 0 12px 30px rgba(0, 0, 0, 0.45);
                                                 text-align: center;
                                                 max-width: 460px;
                                                 border: 1px solid rgba(255, 255, 255, 0.08);
                                               }

                                               .pill {
                                                 display: inline-block;
                                                 padding: 8px 12px;
                                                 border-radius: 999px;
                                                 background: rgba(194, 162, 58, 0.12);
                                                 border: 1px solid rgba(194, 162, 58, 0.28);
                                                 color: #e5ca6f;
                                                 font-size: 12px;
                                                 font-weight: 600;
                                                 letter-spacing: 0.04em;
                                                 margin-bottom: 16px;
                                               }

                                               h1 {
                                                 margin: 0 0 12px 0;
                                                 font-size: 28px;
                                               }

                                               p {
                                                 color: #c8c8c8;
                                                 line-height: 1.5;
                                                 margin: 0;
                                               }

                                               .hint {
                                                 margin-top: 16px;
                                                 font-size: 13px;
                                                 color: #9e9e9e;
                                               }


                                               #fallbackMessage {
                                                 display: none;
                                                 margin-top: 14px;
                                                 font-size: 13px;
                                                 color: #d8b4b4;
                                               }
                                             </style>

                                             <script>
                                               (function () {
                                                 function notifyOpener() {
                                                   try {
                                                     if (window.opener && !window.opener.closed) {
                                                       window.opener.postMessage(
                                                         { type: "PACKTRACKER_AUTH_SUCCESS" },
                                                         "*"
                                                       );
                                                     }
                                                   } catch (e) {
                                                     console.warn("Unable to notify opener:", e);
                                                   }
                                                 }

                                                 function showFallback() {
                                                   var fallback = document.getElementById("fallbackMessage");
                                                   if (fallback) {
                                                     fallback.style.display = "block";
                                                   }
                                                 }

                                                 function tryClose() {
                                                   try {
                                                     window.close();
                                                   } catch (e) {
                                                     console.warn("Direct close failed:", e);
                                                   }

                                                   try {
                                                     window.open("", "_self");
                                                     window.close();
                                                   } catch (e) {
                                                     console.warn("Self-target close failed:", e);
                                                   }

                                                   try {
                                                     self.close();
                                                   } catch (e) {
                                                     console.warn("Self close failed:", e);
                                                   }
                                                 }

                                                 function startCloseSequence() {
                                                   notifyOpener();
                                                   tryClose();

                                                   setTimeout(tryClose, 300);
                                                   setTimeout(tryClose, 800);
                                                   setTimeout(tryClose, 1500);

                                                   setTimeout(function () {
                                                     if (!window.closed) {
                                                       showFallback();
                                                     }
                                                   }, 1800);
                                                 }

                                                 if (document.readyState === "loading") {
                                                   document.addEventListener("DOMContentLoaded", startCloseSequence);
                                                 } else {
                                                   startCloseSequence();
                                                 }
                                               })();
                                             </script>
                                           </head>
                                           <body>
                                             <div class="card">
                                               <div class="pill">HOUSE WOLF // AUTHORIZED</div>
                                               <h1>Discord authentication complete</h1>
                                               <p>PackTracker has received your login. You can return to the desktop app now.</p>
                                               <p class="hint">
                                                 This tab will try to close automatically. If your browser blocks it,
                                                 use the button below or close the tab manually.
                                               </p>
                                                  <button style="padding:10px 20px;border:none;border-radius:6px;background:#c2a23a;color:#000;font-weight:bold;margin-top:20px;" onclick="window.close()">Close Window</button>
                                               <p id="fallbackMessage">
                                                 Your browser blocked automatic tab closing. Please close this tab manually.
                                               </p>
                                             </div>
                                           </body>
                                           </html>
                                           """;

    public record RefreshTokenRequest(string RefreshToken);

    public record LoginTokenPayload(string access_token, string refresh_token, int expires_in);
}
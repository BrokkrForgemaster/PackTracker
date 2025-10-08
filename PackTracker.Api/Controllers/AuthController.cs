using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using PackTracker.Infrastructure.Security;
using PackTracker.Infrastructure.Persistence;


namespace PackTracker.Api.Controllers;

/// <summary name="AuthController">
/// Controller for handling authentication and JWT token management.
/// Provides endpoints for login, token refresh, logout, and fetching user profile info.
/// Requires Discord OAuth for login and issues JWTs for authenticated sessions.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly JwtTokenService _jwt;
    private readonly AppDbContext _db;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IConfiguration config, JwtTokenService jwt, AppDbContext db, ILogger<AuthController> logger)
    {
        _config = config;
        _jwt = jwt;
        _db = db;
        _logger = logger;
    }

    [HttpGet("login")]
    public IActionResult Login()
    {
        _logger.LogInformation("Initiating Discord OAuth login redirect.");
        return Challenge(new AuthenticationProperties { RedirectUri = "/swagger" }, "Discord");
    }

    [Authorize(AuthenticationSchemes = "Cookies")]
    [HttpGet("token")]
    public async Task<IActionResult> GetToken(CancellationToken ct)
    {
        var key = _config["Jwt:Key"];
        if (string.IsNullOrEmpty(key))
        {
            _logger.LogWarning("JWT signing key missing from configuration.");
            return Problem("JWT signing key not configured.", statusCode: 500);
        }

        try
        {
            var discordId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            var username = User.Identity?.Name ?? "";

            var user = await _db.Profiles.AsNoTracking()
                .FirstOrDefaultAsync(p => p.DiscordId == discordId, ct);

            if (user == null)
            {
                _logger.LogWarning("Unauthorized token request. DiscordId={DiscordId}", discordId);
                return Unauthorized(new { error = "User not registered." });
            }

            var accessToken = _jwt.GenerateAccessToken(user);
            var refreshToken = await _jwt.GenerateRefreshTokenAsync(user.Id, ct);
            var expiresIn = _config.GetValue<int>("Jwt:ExpiresInMinutes", 60) * 60;

            _logger.LogInformation("JWT issued for {Username} ({DiscordId})", username, discordId);

            return Ok(new
            {
                access_token = accessToken,
                refresh_token = refreshToken.Token,
                expires_in = expiresIn
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating JWT access token.");
            return Problem("An error occurred while generating the token.");
        }
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            return BadRequest(new { error = "Missing refresh token." });

        try
        {
            var user = await _jwt.ValidateRefreshTokenAsync(request.RefreshToken, ct);
            if (user == null)
            {
                _logger.LogWarning("Invalid refresh token attempt.");
                return Unauthorized();
            }

            var accessToken = _jwt.GenerateAccessToken(user);
            var newRefresh = await _jwt.GenerateRefreshTokenAsync(user.Id, ct);

            await _jwt.RevokeRefreshTokenAsync(request.RefreshToken, ct);

            _logger.LogInformation("Refreshed tokens for user {UserId}", user.Id);

            return Ok(new
            {
                access_token = accessToken,
                refresh_token = newRefresh.Token,
                expires_in = _config.GetValue<int>("Jwt:ExpiresInMinutes", 60) * 60
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing JWT.");
            return Problem("Token refresh failed.");
        }
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenRequest request, CancellationToken ct)
    {
        try
        {
            await _jwt.RevokeRefreshTokenAsync(request.RefreshToken, ct);
            _logger.LogInformation("User {UserId} logged out and refresh token revoked.", User.FindFirstValue(ClaimTypes.NameIdentifier));
            return Ok(new { message = "Logged out successfully." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Logout failed.");
            return Problem("Error logging out user.");
        }
    }

    [Authorize]
    [HttpGet("me")]
    public IActionResult Me()
    {
        var claims = User.Claims.Select(c => new { c.Type, c.Value });
        _logger.LogInformation("Fetched profile info for {Username}", User.Identity?.Name);
        return Ok(new
        {
            User.Identity?.Name,
            Claims = claims
        });
    }

    public record RefreshTokenRequest(string RefreshToken);
}

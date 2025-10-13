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
    # region Fields and Constructor
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
    # endregion


    #region Endpoints
    /// <summary name="Login">
    /// Initiates the Discord OAuth login process,
    /// redirecting the user to Discord's authorization page.
    /// </summary>
    /// <returns>
    /// A redirect to Discord's OAuth authorization endpoint
    /// or an error response if configuration is missing.
    /// </returns>
    [HttpGet("login")]
    public IActionResult Login()
    {
        _logger.LogInformation("Initiating Discord OAuth login redirect.");
        return Challenge(new AuthenticationProperties { RedirectUri = "/swagger" }, "Discord");
    }

    /// <summary name="GetToken">
    /// Issues a JWT access token and refresh token for the authenticated user.
    /// Requires the user to be authenticated via cookies (post-OAuth).
    /// </summary>
    /// <param name="ct">
    /// Cancellation token for the async operation.
    /// </param>
    /// <returns>
    /// A JSON response containing the access token, refresh token, and expiration info,
    /// or an error response if the user is not registered or an error occurs.
    /// </returns>
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
    
    /// <summary name="Refresh">
    /// Refreshes the JWT access token using a valid refresh token.
    /// </summary>
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

    /// <summary name="Logout">
    /// Logs out the user by revoking the provided refresh token.
    /// Requires the user to be authenticated.
    /// </summary>
    /// <param name="request">
    /// The request containing the refresh token to revoke.
    /// </param>
    /// <param name="ct">
    ///
    /// </param>
    /// <returns></returns>
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

    /// <summary name="Me">
    /// Fetches the profile information of the authenticated user,
    /// including their claims.
    /// </summary>
    /// <returns>
    /// A JSON response containing the user's username and claims,
    /// or an unauthorized response if not authenticated.
    /// </returns>
    /// </summary>
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
    #endregion

    #region Records
    
    /// <summary name="RefreshTokenRequest">
    /// Request model for refreshing JWT tokens using a refresh token.
    /// </summary>
    /// <param name="RefreshToken">
    /// The refresh token issued during initial authentication or previous refresh.
    /// </param>
    public record RefreshTokenRequest(
        string RefreshToken);
    #endregion 
    
}

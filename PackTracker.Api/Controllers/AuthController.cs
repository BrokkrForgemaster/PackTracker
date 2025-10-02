using System.Text;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using PackTracker.Infrastructure.Security;
using PackTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace PackTracker.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly JwtTokenService _jwt;
    private readonly AppDbContext _db;

    public AuthController(IConfiguration config, JwtTokenService jwt, AppDbContext db)
    {
        _config = config;
        _jwt = jwt;
        _db = db;
    }

    [HttpGet("login")]
    public IActionResult Login()
    {
        return Challenge(new AuthenticationProperties { RedirectUri = "/swagger" }, "Discord");
    }

    [Authorize(AuthenticationSchemes = "Cookies")]
    [HttpGet("token")]
    public async Task<IActionResult> GetToken(CancellationToken ct)
    {
        var key = _config["Jwt:Key"];
        if (string.IsNullOrEmpty(key))
        {
            return Problem("JWT signing key not configured.");
        }

        var discordId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        var username = User.Identity?.Name ?? "";

        var user = await _db.Profiles.FirstOrDefaultAsync(p => p.DiscordId == discordId, ct);
        if (user == null) return Unauthorized();

        var accessToken = _jwt.GenerateAccessToken(user);
        var refreshToken = await _jwt.GenerateRefreshTokenAsync(user.Id, ct);

        return Ok(new
        {
            access_token = accessToken,
            refresh_token = refreshToken.Token,
            expires_in = 3600
        });
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] string refreshToken, CancellationToken ct)
    {
        var user = await _jwt.ValidateRefreshTokenAsync(refreshToken, ct);
        if (user == null) return Unauthorized();

        var accessToken = _jwt.GenerateAccessToken(user);
        var newRefresh = await _jwt.GenerateRefreshTokenAsync(user.Id, ct);

        // revoke old refresh
        await _jwt.RevokeRefreshTokenAsync(refreshToken, ct);

        return Ok(new
        {
            access_token = accessToken,
            refresh_token = newRefresh.Token,
            expires_in = 3600
        });
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] string refreshToken, CancellationToken ct)
    {
        await _jwt.RevokeRefreshTokenAsync(refreshToken, ct);
        return Ok(new { message = "Logged out and refresh token revoked" });
    }

    [Authorize]
    [HttpGet("me")]
    public IActionResult Me()
    {
        var claims = User.Claims.Select(c => new { c.Type, c.Value });
        return Ok(new
        {
            User.Identity?.Name,
            Claims = claims
        });
    }
}

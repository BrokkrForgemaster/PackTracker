using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace PackTracker.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _config;

    public AuthController(IConfiguration config)
    {
        _config = config;
    }

    /// <summary>
    /// Redirect to Discord login.
    /// </summary>
    [HttpGet("login")]
    public IActionResult Login()
    {
        return Challenge(new AuthenticationProperties { RedirectUri = "/swagger" }, "Discord");
    }

    /// <summary>
    /// Get the current authenticated user’s claims.
    /// </summary>
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

    /// <summary>
    /// Issue a PackTracker JWT for API calls.
    /// </summary>
    [Authorize]
    [HttpGet("token")]
    public IActionResult GetToken()
    {
        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty),
            new Claim(JwtRegisteredClaimNames.UniqueName, User.Identity?.Name ?? string.Empty)
        };

        // include all original claims (roles, avatar, etc.)
        claims.AddRange(User.Claims.Where(c =>
            c.Type != ClaimTypes.NameIdentifier &&
            c.Type != ClaimTypes.Name));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var jwt = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: creds);

        var tokenString = new JwtSecurityTokenHandler().WriteToken(jwt);

        return Ok(new { token = tokenString });
    }

    /// <summary>
    /// Logout and clear local cookies.
    /// </summary>
    [Authorize]
    [HttpPost("logout")]
    public IActionResult Logout()
    {
        return SignOut(new AuthenticationProperties { RedirectUri = "/" },
            new[] { "Cookies", "Discord" });
    }
}

using System.Text;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;

namespace PackTracker.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _config;

    public AuthController(IConfiguration config)
    {
        _config = config;
    }

    [HttpGet("login")]
    public IActionResult Login()
    {
        // This triggers Discord OAuth challenge
        return Challenge(new AuthenticationProperties { RedirectUri = "/swagger" }, "Discord");
    }

    [Authorize(AuthenticationSchemes = "Cookies")]
    [HttpGet("token")]
    public IActionResult GetToken()
    {
        var key = _config["Jwt:Key"];
        if (string.IsNullOrEmpty(key))
        {
            return Problem("JWT signing key not configured.");
        }

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, User.FindFirstValue(ClaimTypes.NameIdentifier) ?? ""),
            new Claim(JwtRegisteredClaimNames.UniqueName, User.Identity?.Name ?? ""),
            new Claim(ClaimTypes.Role, "HouseWolfMember")
        };

        var creds = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            SecurityAlgorithms.HmacSha256
        );

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds
        );

        return Ok(new
        {
            access_token = new JwtSecurityTokenHandler().WriteToken(token),
            expires_in = 3600
        });
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

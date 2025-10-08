using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using PackTracker.Domain.Entities;
using PackTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace PackTracker.Infrastructure.Security;

public class JwtTokenService
{
    private readonly IConfiguration _config;
    private readonly AppDbContext _db;

    public JwtTokenService(IConfiguration config, AppDbContext db)
    {
        _config = config;
        _db = db;
    }

    /// <summary>
    /// Generate a short-lived access token (JWT).
    /// </summary>
    public string GenerateAccessToken(Profile user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.DiscordId ?? ""),
            new Claim(ClaimTypes.Name, user.Username ?? ""),
            new Claim(ClaimTypes.Role, "HouseWolfMember")
        };

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Generate and persist a refresh token.
    /// </summary>
    public async Task<RefreshToken> GenerateRefreshTokenAsync(Guid userId, CancellationToken ct)
    {
        var refresh = new RefreshToken
        {
            UserId = userId,
            Token = Convert.ToBase64String(Guid.NewGuid().ToByteArray()),
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };

        _db.RefreshTokens.Add(refresh);
        await _db.SaveChangesAsync(ct);

        return refresh;
    }

    /// <summary>
    /// Validate a refresh token and return its user, or null if invalid.
    /// </summary>
    public async Task<Profile?> ValidateRefreshTokenAsync(string token, CancellationToken ct)
    {
        var refresh = await _db.RefreshTokens
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Token == token, ct);

        if (refresh == null || refresh.IsRevoked)
            return null;

        return await _db.Profiles.FindAsync(new object[] { refresh.UserId }, ct);
    }

    /// <summary>
    /// Revoke a refresh token so it can’t be reused.
    /// </summary>
    public async Task RevokeRefreshTokenAsync(string token, CancellationToken ct)
    {
        var refresh = await _db.RefreshTokens.FirstOrDefaultAsync(r => r.Token == token, ct);
        if (refresh != null && !refresh.IsRevoked)
        {
            refresh.RevokedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
    }
}

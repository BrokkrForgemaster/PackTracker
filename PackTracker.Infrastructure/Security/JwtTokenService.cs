using System.IdentityModel.Tokens.Jwt;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Entities;
using PackTracker.Infrastructure.Persistence;

using Microsoft.Extensions.Options;
using PackTracker.Application.Options;
using PackTracker.Domain.Security;

namespace PackTracker.Infrastructure.Security;

public class JwtTokenService
{
    private readonly AppDbContext _db;
    private readonly ILogger<JwtTokenService> _logger;
    private readonly string _jwtKey;
    private readonly string _jwtIssuer;
    private readonly string _jwtAudience;
    private readonly int _accessTokenMinutes;
    private readonly int _refreshTokenDays;

    public JwtTokenService(
        IOptions<AuthOptions> authOptions,
        AppDbContext db,
        ILogger<JwtTokenService> logger)
    {
        _db = db;
        _logger = logger;

        var options = authOptions.Value.Jwt;

        _jwtKey = options.Key ?? string.Empty;
        if (_jwtKey.Length < 16)
            throw new InvalidOperationException("JWT key too short; must be at least 16 characters.");

        _jwtIssuer = string.IsNullOrWhiteSpace(options.Issuer) ? "PackTracker" : options.Issuer;
        _jwtAudience = string.IsNullOrWhiteSpace(options.Audience) ? "PackTrackerClient" : options.Audience;
        _accessTokenMinutes = options.ExpiresInMinutes > 0 ? options.ExpiresInMinutes : 60;
        _refreshTokenDays = options.RefreshTokenDays > 0 ? options.RefreshTokenDays : 30;
    }

    public string GenerateAccessToken(Profile user)
    {
        var keyBytes = Encoding.UTF8.GetBytes(_jwtKey);
        var creds = new SigningCredentials(new SymmetricSecurityKey(keyBytes), SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.DiscordId),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Role, SecurityConstants.Roles.HouseWolfMember),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        if (!string.IsNullOrWhiteSpace(user.DiscordRank))
        {
            claims.Add(new Claim(ClaimTypes.Role, user.DiscordRank));
        }

        if (!string.IsNullOrWhiteSpace(user.DiscordDivision))
        {
            claims.Add(new Claim("urn:discord:division", user.DiscordDivision));
        }

        var token = new JwtSecurityToken(
            issuer: _jwtIssuer,
            audience: _jwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_accessTokenMinutes),
            signingCredentials: creds
        );

        var jwt = new JwtSecurityTokenHandler().WriteToken(token);
        _logger.LogInformation("✅ Issued JWT for {User} expiring in {Minutes} min.", user.Username, _accessTokenMinutes);
        return jwt;
    }

    public async Task<RefreshToken> GenerateRefreshTokenAsync(Guid userId, CancellationToken ct)
    {
        var refresh = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Token = Convert.ToBase64String(Guid.NewGuid().ToByteArray())
                       .Replace("+", "")
                       .Replace("/", "")
                       .Replace("=", ""),
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(_refreshTokenDays),
            IsRevoked = false
        };

        await _db.RefreshTokens.AddAsync(refresh, ct);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("✅ Created refresh token for user {UserId}, expiring in {Days} days.", userId, _refreshTokenDays);
        return refresh;
    }

    public async Task<Profile?> ValidateRefreshTokenAsync(string token, CancellationToken ct)
    {
        var refresh = await _db.RefreshTokens
            .AsNoTracking()
            .Include(r => r.Profile)
            .FirstOrDefaultAsync(r => r.Token == token && !r.IsRevoked, ct);

        if (refresh == null)
        {
            _logger.LogWarning("❌ Invalid or revoked refresh token attempted.");
            return null;
        }

        if (refresh.ExpiresAt < DateTime.UtcNow)
        {
            _logger.LogWarning("⚠️ Expired refresh token used for {UserId}.", refresh.UserId);
            return null;
        }

        _logger.LogInformation("✅ Valid refresh token for {UserId}.", refresh.UserId);
        return refresh.Profile;
    }

    public async Task RevokeRefreshTokenAsync(string token, CancellationToken ct)
    {
        var refresh = await _db.RefreshTokens.FirstOrDefaultAsync(r => r.Token == token, ct);
        if (refresh == null) return;

        refresh.IsRevoked = true;
        refresh.RevokedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("🚫 Revoked refresh token for user {UserId}", refresh.UserId);
    }

    public async Task<(string accessToken, string refreshToken, int expiresIn)> IssueTokenPairAsync(Profile user, CancellationToken ct)
    {
        var accessToken = GenerateAccessToken(user);
        var refresh = await GenerateRefreshTokenAsync(user.Id, ct);

        return (accessToken, refresh.Token, _accessTokenMinutes * 60);
    }
}

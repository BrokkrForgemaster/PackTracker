using System.IdentityModel.Tokens.Jwt;

public static class JwtHelper
{
    public static bool IsTokenExpired(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return true;

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);
        var exp = jwt.ValidTo;

        return exp < DateTime.UtcNow;
    }
}
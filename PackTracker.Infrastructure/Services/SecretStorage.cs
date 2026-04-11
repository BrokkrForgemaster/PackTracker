using System.Security.Cryptography;
using System.Text;

public static class SecretStorage
{
    public static string Protect(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        try
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            var encrypted = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(encrypted);
        }
        catch (Exception ex) when (ex is CryptographicException || ex is PlatformNotSupportedException)
        {
            // DPAPI unavailable (non-Windows platform, no user profile, service context, etc.)
            Console.WriteLine($"DPAPI protect failed: {ex.Message}");
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
        }
    }

    public static string Unprotect(string? protectedValue)
    {
        if (string.IsNullOrWhiteSpace(protectedValue))
            return string.Empty;

        try
        {
            // Try DPAPI first (standard behavior)
            var bytes = Convert.FromBase64String(protectedValue);
            var decrypted = ProtectedData.Unprotect(bytes, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(decrypted);
        }
        catch (Exception ex) when (ex is CryptographicException || ex is FormatException || ex is PlatformNotSupportedException)
        {
            // Fallback: Check if it's a raw base64 string (not DPAPI encrypted)
            try
            {
                var plain = Convert.FromBase64String(protectedValue);
                return Encoding.UTF8.GetString(plain);
            }
            catch
            {
                // Final fallback: It's just a plain string
                return protectedValue;
            }
        }
    }
    }
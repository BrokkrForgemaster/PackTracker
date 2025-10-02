using System.Security.Cryptography;
using System.Text;

namespace PackTracker.Infrastructure.Services
{
    public static class SecretStorage
    {
        public static string Protect(string? plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return string.Empty;
            var bytes = Encoding.UTF8.GetBytes(plainText);
            var enc = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(enc, Base64FormattingOptions.None);
        }

        public static string Unprotect(string? protectedText)
        {
            if (string.IsNullOrEmpty(protectedText)) return string.Empty;
            var bytes = Convert.FromBase64String(protectedText);
            var dec = ProtectedData.Unprotect(bytes, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(dec, 0, dec.Length);
        }
    }
}
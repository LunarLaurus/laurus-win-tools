using System.Security.Cryptography;
using System.Text;

namespace ProgramHider;

// Hashing and verification helpers for restore PIN/password flows.
internal static class PinSecurity
{
    internal static string HashSecret(string secret)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(secret.Trim());
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hash);
    }

    internal static bool VerifySecret(string secret, string expectedHash)
    {
        if (string.IsNullOrWhiteSpace(expectedHash))
        {
            return false;
        }

        var actualHash = HashSecret(secret);
        return string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase);
    }
}

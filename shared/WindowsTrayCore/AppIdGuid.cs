using System.Security.Cryptography;
using System.Text;

namespace WindowsTrayCore;

/// <summary>
/// Deterministic Guid derivation from a stable app identifier. Used to give
/// each tray icon a permanent identity so Windows remembers its overflow /
/// show / hide preference across rebuilds and version bumps.
/// </summary>
public static class AppIdGuid
{
    // Fixed namespace seed for this project. Never change — it's part of the
    // Guid input. Changing this would re-randomise every app's tray identity.
    private static readonly byte[] _namespaceSeed = Encoding.UTF8.GetBytes(
        "laurus-win-tools::tray-icon::v1");

    /// <summary>
    /// Returns the same Guid every time for the same <paramref name="appId"/>.
    /// Two different app ids produce two different Guids with overwhelming
    /// probability (SHA-256 collisions).
    /// </summary>
    public static Guid For(string appId)
    {
        if (string.IsNullOrWhiteSpace(appId))
            throw new ArgumentException("App id must be non-empty.", nameof(appId));

        Span<byte> hash = stackalloc byte[32];
        using var sha = SHA256.Create();
        sha.TransformBlock(_namespaceSeed, 0, _namespaceSeed.Length, null, 0);
        var idBytes = Encoding.UTF8.GetBytes(appId);
        sha.TransformFinalBlock(idBytes, 0, idBytes.Length);
        sha.Hash!.AsSpan(0, 32).CopyTo(hash);

        // Take first 16 bytes; force RFC-4122 variant + version-5 bits so the
        // resulting Guid is a well-formed name-based UUID. (The variant bits
        // aren't strictly required for our local use but keep the output
        // looking like a normal UUID in logs and registry dumps.)
        Span<byte> bytes = stackalloc byte[16];
        hash[..16].CopyTo(bytes);
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x50); // version 5
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80); // RFC 4122 variant

        return new Guid(bytes);
    }
}

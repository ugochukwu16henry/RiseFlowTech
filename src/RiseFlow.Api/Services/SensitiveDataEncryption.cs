using System.Security.Cryptography;

namespace RiseFlow.Api.Services;

/// <summary>
/// Encrypts/decrypts sensitive fields at rest (NIN, phone numbers, etc.).
/// Call Initialize(key) at startup. If no key is set, values are stored and read as plaintext (backward compatible).
/// Stored format when encryption is on: "RFENC:" + Base64(AES-256-GCM ciphertext including nonce and tag).
/// </summary>
public static class SensitiveDataEncryption
{
    private const string Prefix = "RFENC:";
    private static byte[]? _key;
    private static readonly object Lock = new();

    public static void Initialize(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return;
        var decoded = Convert.FromBase64String(key.Trim());
        if (decoded.Length != 32)
            return;
        lock (Lock)
        {
            _key = decoded;
        }
    }

    public static bool IsEnabled
    {
        get { lock (Lock) { return _key != null && _key.Length == 32; } }
    }

    /// <summary>Encrypts value for storage. Returns plaintext if no key or value is null/empty.</summary>
    public static string? Encrypt(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return value;
        byte[]? key;
        lock (Lock) { key = _key; }
        if (key == null || key.Length != 32)
            return value;

        try
        {
            var plain = System.Text.Encoding.UTF8.GetBytes(value);
            var nonce = new byte[12];
            RandomNumberGenerator.Fill(nonce);
            var cipher = new byte[plain.Length];
            var tag = new byte[16];
            using (var aes = new AesGcm(key, 16))
            {
                aes.Encrypt(nonce, plain, cipher, tag);
            }
            var combined = new byte[nonce.Length + cipher.Length + tag.Length];
            Buffer.BlockCopy(nonce, 0, combined, 0, nonce.Length);
            Buffer.BlockCopy(cipher, 0, combined, nonce.Length, cipher.Length);
            Buffer.BlockCopy(tag, 0, combined, nonce.Length + cipher.Length, tag.Length);
            return Prefix + Convert.ToBase64String(combined);
        }
        catch
        {
            return value;
        }
    }

    /// <summary>Decrypts value from storage. Returns as-is if not encrypted or decryption fails (backward compat).</summary>
    public static string? Decrypt(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return value;
        if (!value.StartsWith(Prefix, StringComparison.Ordinal))
            return value;
        byte[]? key;
        lock (Lock) { key = _key; }
        if (key == null || key.Length != 32)
            return value;

        try
        {
            var combined = Convert.FromBase64String(value.Substring(Prefix.Length));
            if (combined.Length < 12 + 16 + 1)
                return value;
            var nonce = new byte[12];
            var tag = new byte[16];
            var cipher = new byte[combined.Length - 12 - 16];
            Buffer.BlockCopy(combined, 0, nonce, 0, 12);
            Buffer.BlockCopy(combined, 12, cipher, 0, cipher.Length);
            Buffer.BlockCopy(combined, 12 + cipher.Length, tag, 0, 16);
            var plain = new byte[cipher.Length];
            using (var aes = new AesGcm(key, 16))
            {
                aes.Decrypt(nonce, cipher, tag, plain);
            }
            return System.Text.Encoding.UTF8.GetString(plain);
        }
        catch
        {
            return value;
        }
    }

    /// <summary>Generate a 256-bit key (Base64) for config. Run once and set in Configuration["Encryption:Key"].</summary>
    public static string GenerateKeyBase64()
    {
        var key = new byte[32];
        RandomNumberGenerator.Fill(key);
        return Convert.ToBase64String(key);
    }
}

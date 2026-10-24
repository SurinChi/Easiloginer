using System.Security.Cryptography;
using System.Text;

namespace EasiLoginer.Infrastructure.Security;

/// <summary>
/// Fernet 兼容的对称加密器（AES-128-CBC + HMAC-SHA256）。
/// </summary>
/// <remarks>
/// <para>令牌布局与 <c>cryptography.fernet.Fernet</c> 完全一致：</para>
/// <code>
/// token = 0x80 | timestamp(8B, 大端) | IV(16B) | AES-128-CBC(PKCS7) | HMAC-SHA256(前四段)
/// 密钥(32B)：signingKey = key[0..16]，encryptionKey = key[16..32]
/// </code>
/// <para>
/// 因此 EasiLoginer 能解开上游写出的 <c>ea$</c> 密文，上游也能解开本项目写出的密文
/// （文档 §7.3 / §11.4）。实现上先校验 HMAC 再解密，且比较用固定时间算法。
/// </para>
/// </remarks>
public sealed class FernetCipher
{
    private const byte Version = 0x80;
    private const int HeaderLength = 1 + 8 + 16;
    private const int MacLength = 32;

    private readonly byte[] _signingKey;
    private readonly byte[] _encryptionKey;

    /// <summary>以 32 字节原始密钥创建加密器。</summary>
    public FernetCipher(ReadOnlySpan<byte> key)
    {
        if (key.Length != 32)
        {
            throw new ArgumentException("Fernet 密钥必须是 32 字节。", nameof(key));
        }

        _signingKey = key[..16].ToArray();
        _encryptionKey = key[16..].ToArray();
    }

    /// <summary>用 URL-safe Base64 密钥字符串创建加密器（上游 <c>fernet_key</c> 的形式）。</summary>
    public static FernetCipher FromBase64Key(string base64UrlKey)
        => new(Base64Url.Decode(base64UrlKey));

    /// <summary>加密数据，返回 URL-safe Base64 令牌。</summary>
    public string Encrypt(ReadOnlySpan<byte> plaintext, DateTimeOffset? timestamp = null)
    {
        var issuedAt = (timestamp ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds();

        var iv = new byte[16];
        RandomNumberGenerator.Fill(iv);

        byte[] ciphertext;
        using (var aes = Aes.Create())
        {
            aes.Key = _encryptionKey;
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var encryptor = aes.CreateEncryptor();
            ciphertext = encryptor.TransformFinalBlock(plaintext.ToArray(), 0, plaintext.Length);
        }

        var token = new byte[HeaderLength + ciphertext.Length + MacLength];
        token[0] = Version;
        System.Buffers.Binary.BinaryPrimitives.WriteInt64BigEndian(token.AsSpan(1, 8), issuedAt);
        iv.CopyTo(token, 9);
        ciphertext.CopyTo(token, HeaderLength);

        var mac = HMACSHA256.HashData(_signingKey, token.AsSpan(0, HeaderLength + ciphertext.Length));
        mac.CopyTo(token, HeaderLength + ciphertext.Length);

        return Base64Url.Encode(token);
    }

    /// <summary>解密 URL-safe Base64 令牌。</summary>
    /// <exception cref="CryptographicException">令牌格式非法、被篡改或密钥不匹配。</exception>
    public byte[] Decrypt(string token)
    {
        var data = Base64Url.Decode(token);
        if (data.Length < HeaderLength + MacLength)
        {
            throw new CryptographicException("令牌长度不足，无法解密。");
        }

        if (data[0] != Version)
        {
            throw new CryptographicException($"不支持的令牌版本：0x{data[0]:X2}。");
        }

        var macOffset = data.Length - MacLength;
        var expectedMac = HMACSHA256.HashData(_signingKey, data.AsSpan(0, macOffset));
        if (!CryptographicOperations.FixedTimeEquals(expectedMac, data.AsSpan(macOffset, MacLength)))
        {
            throw new CryptographicException("令牌校验失败：密钥不匹配或数据被篡改。");
        }

        var iv = data.AsSpan(9, 16).ToArray();
        var ciphertext = data.AsSpan(HeaderLength, macOffset - HeaderLength).ToArray();

        using var aes = Aes.Create();
        aes.Key = _encryptionKey;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var decryptor = aes.CreateDecryptor();
        return decryptor.TransformFinalBlock(ciphertext, 0, ciphertext.Length);
    }

    /// <summary>读取令牌中的签发时间（不校验内容，仅用于诊断）。</summary>
    public static DateTimeOffset ReadTimestamp(string token)
    {
        var data = Base64Url.Decode(token);
        if (data.Length < 9)
        {
            throw new CryptographicException("令牌长度不足，无法读取时间戳。");
        }

        var seconds = System.Buffers.Binary.BinaryPrimitives.ReadInt64BigEndian(data.AsSpan(1, 8));
        return DateTimeOffset.FromUnixTimeSeconds(seconds);
    }
}

/// <summary>URL-safe Base64（与 Python <c>base64.urlsafe_b64encode</c> 一致：保留 <c>=</c> 填充）。</summary>
internal static class Base64Url
{
    public static string Encode(ReadOnlySpan<byte> data)
        => Convert.ToBase64String(data).Replace('+', '-').Replace('/', '_');

    public static byte[] Decode(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var normalized = text.Replace('-', '+').Replace('_', '/');
        return (normalized.Length % 4) switch
        {
            0 => Convert.FromBase64String(normalized),
            2 => Convert.FromBase64String(normalized + "=="),
            3 => Convert.FromBase64String(normalized + "="),
            _ => throw new CryptographicException("非法的 Base64 令牌。"),
        };
    }
}

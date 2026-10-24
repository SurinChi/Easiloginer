using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;

namespace EasiLoginer.Infrastructure.Security;

/// <summary>
/// 主密码派生（Argon2id），参数必须与上游 <c>core/security.py</c> 完全一致。
/// </summary>
/// <remarks>
/// <code>
/// salt       = 16 字节随机
/// memory     = 65536 KiB
/// iterations = 3
/// lanes      = 4
/// output     = 32 字节
/// fernetKey  = base64.urlsafe_b64encode(output)   // 保留 '=' 填充
/// </code>
/// 参数改动会让所有既有加密档案无法解密（文档 §7.3），因此全部以常量固定。
/// </remarks>
public static class MasterKeyDeriver
{
    /// <summary>Argon2id 内存开销（KiB）。</summary>
    public const int MemoryCostKib = 65536;

    /// <summary>Argon2id 迭代次数。</summary>
    public const int Iterations = 3;

    /// <summary>Argon2id 并行度。</summary>
    public const int Lanes = 4;

    /// <summary>派生密钥长度（字节）。</summary>
    public const int KeyLength = 32;

    /// <summary>盐长度（字节）。</summary>
    public const int SaltLength = 16;

    /// <summary>派生 32 字节原始密钥。</summary>
    public static byte[] DeriveRaw(string masterPassword, ReadOnlySpan<byte> salt)
    {
        ArgumentNullException.ThrowIfNull(masterPassword);
        if (salt.Length != SaltLength)
        {
            throw new ArgumentException($"盐必须是 {SaltLength} 字节。", nameof(salt));
        }

        var passwordBytes = Encoding.UTF8.GetBytes(masterPassword);
        try
        {
            using var argon2 = new Argon2id(passwordBytes)
            {
                Salt = salt.ToArray(),
                DegreeOfParallelism = Lanes,
                MemorySize = MemoryCostKib,
                Iterations = Iterations,
            };

            return argon2.GetBytes(KeyLength);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passwordBytes);
        }
    }

    /// <summary>派生 URL-safe Base64 的 Fernet 密钥字符串。</summary>
    public static string DeriveFernetKey(string masterPassword, ReadOnlySpan<byte> salt)
    {
        var raw = DeriveRaw(masterPassword, salt);
        try
        {
            return Convert.ToBase64String(raw).Replace('+', '-').Replace('/', '_');
        }
        finally
        {
            CryptographicOperations.ZeroMemory(raw);
        }
    }

    /// <summary>生成一个新的 16 字节随机盐。</summary>
    public static byte[] CreateSalt() => RandomNumberGenerator.GetBytes(SaltLength);
}

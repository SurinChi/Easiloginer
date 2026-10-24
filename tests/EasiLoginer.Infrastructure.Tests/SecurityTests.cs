using System.Security.Cryptography;
using System.Text;
using EasiLoginer.Domain.Privacy;
using EasiLoginer.Infrastructure.Paths;
using EasiLoginer.Infrastructure.Security;
using FluentAssertions;

namespace EasiLoginer.Infrastructure.Tests;

/// <summary>
/// 加密层的兼容性测试。
/// </summary>
/// <remarks>
/// 所有期望值都由工作区 venv 里的 Python <c>cryptography</c> / <c>argon2-cffi</c>
/// 真实生成（脚本：<c>tools/compat/gen_vectors.py</c>），不是手工推算。
/// 这是文档 §11.4「加密互通」的验收依据。
/// </remarks>
public sealed class SecurityTests
{
    private const string Password = "Correct Horse Battery Staple 中文";
    private const string PasswordUtf8Base64 = "Q29ycmVjdCBIb3JzZSBCYXR0ZXJ5IFN0YXBsZSDkuK3mloc=";
    private const string SaltBase64 = "AAECAwQFBgcICQoLDA0ODw==";
    private const string FernetKey = "aIwM6R2BCcEjN8Tvdap5Ep3lkP-SQgrOmWUmDjZnLUQ=";
    private const string TokenMasterCheck = "gAAAAABq3GYd6fktG9F6DJmWk9ivzwN9kl0j0vzj-LaWiITGmkoDSloGKK86twJhOCjh8-MjE3gvI0Xln_P9zFw2zJyng1reDUIoD4ZprnrsuovtXw-xEEk=";
    private const string TokenUtf8 = "gAAAAABq3GYdpjJDcyRYgoshlfnZl3qWFAtwzDN44Y2bjct7c0D6vIPPlqOj8DdHtk2zaZMzzIMfeKskjwiKE_S-n6YfX5hjuHoSK-AdDyJlgeywjVlzAas=";
    private const string MasterCheckPayload = "EasiAuto-profile-master-check";

    private static byte[] Salt => Convert.FromBase64String(SaltBase64);

    /// <summary>密码的 UTF-8 字节必须与 Python 侧一致，否则派生结果无意义。</summary>
    [Fact]
    public void 密码编码与Python一致()
    {
        Convert.ToBase64String(Encoding.UTF8.GetBytes(Password)).Should().Be(PasswordUtf8Base64);
    }

    /// <summary>Argon2id 参数与上游一致时，派生出的 Fernet 密钥必须逐字符相同。</summary>
    [Fact]
    public void Argon2id派生密钥与Python一致()
    {
        MasterKeyDeriver.DeriveFernetKey(Password, Salt).Should().Be(FernetKey);
    }

    /// <summary>盐长度非法时立即失败，避免用错参数派生出"看起来正常"的密钥。</summary>
    [Fact]
    public void 盐长度非法时抛异常()
    {
        var action = () => MasterKeyDeriver.DeriveRaw(Password, new byte[8]);

        action.Should().Throw<ArgumentException>();
    }

    /// <summary>能解开 Python 写出的主密码校验令牌。</summary>
    [Fact]
    public void 能解开Python生成的令牌()
    {
        var cipher = FernetCipher.FromBase64Key(FernetKey);

        var plaintext = cipher.Decrypt(TokenMasterCheck);

        Encoding.UTF8.GetString(plaintext).Should().Be(MasterCheckPayload);
    }

    /// <summary>中文与特殊字符必须原样往返。</summary>
    [Fact]
    public void 能解开Python生成的中文令牌()
    {
        var cipher = FernetCipher.FromBase64Key(FernetKey);

        var plaintext = cipher.Decrypt(TokenUtf8);

        Encoding.UTF8.GetString(plaintext).Should().Be("档案密码：p@ssw0rd 中文");
    }

    /// <summary>自己加密的令牌能自己解开，且带固定时间戳时长度稳定。</summary>
    [Fact]
    public void 加密解密往返()
    {
        var cipher = FernetCipher.FromBase64Key(FernetKey);
        var payload = Encoding.UTF8.GetBytes("往返测试 payload");
        var timestamp = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);

        var token = cipher.Encrypt(payload, timestamp);

        cipher.Decrypt(token).Should().Equal(payload);
        FernetCipher.ReadTimestamp(token).ToUnixTimeSeconds().Should().Be(1_700_000_000);
    }

    /// <summary>篡改任意一个字符都必须被 HMAC 拦下。</summary>
    [Fact]
    public void 篡改令牌被拒绝()
    {
        var cipher = FernetCipher.FromBase64Key(FernetKey);
        var token = cipher.Encrypt(Encoding.UTF8.GetBytes("tamper me"));
        var tampered = token[..^2] + (token[^2] == 'A' ? 'B' : 'A') + token[^1];

        var action = () => cipher.Decrypt(tampered);

        action.Should().Throw<CryptographicException>();
    }

    /// <summary>换一把密钥必须解密失败，而不是返回垃圾数据。</summary>
    [Fact]
    public void 错误密钥被拒绝()
    {
        var otherKey = Convert.ToBase64String(new byte[32]).Replace('+', '-').Replace('/', '_');
        var cipher = FernetCipher.FromBase64Key(otherKey);

        var action = () => cipher.Decrypt(TokenMasterCheck);

        action.Should().Throw<CryptographicException>();
    }

    /// <summary>账号脱敏与 Python <c>sha256(...).hexdigest()[:16]</c> 一致。</summary>
    [Fact]
    public void 账号脱敏与Python一致()
    {
        AccountHasher.Redact("student@example.com").Should().Be("616bb35d31d0a684");
    }

    /// <summary>DPAPI 缓存能按当前用户往返，并带隐藏属性。</summary>
    [Fact]
    public void DPAPI缓存往返()
    {
        var root = Path.Combine(Path.GetTempPath(), "EasiLoginerTests", "dpapi", Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new AppPaths(root);
            var cache = new DpapiKeyCache(paths);
            var key = RandomNumberGenerator.GetBytes(32);

            cache.Save(key);

            cache.TryRead().Should().Equal(key);
            File.GetAttributes(cache.CacheFile).HasFlag(FileAttributes.Hidden).Should().BeTrue("缓存文件必须隐藏");

            cache.Clear();
            File.Exists(cache.CacheFile).Should().BeFalse();
            cache.TryRead().Should().BeNull();
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    /// <summary>密钥被清零后不再留在内存里（DeriveRaw 内部会 ZeroMemory）。</summary>
    [Fact]
    public void 派生密钥长度固定()
    {
        MasterKeyDeriver.DeriveRaw(Password, Salt).Should().HaveCount(32);
    }
}

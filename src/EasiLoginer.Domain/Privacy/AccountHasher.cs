using System.Security.Cryptography;
using System.Text;

namespace EasiLoginer.Domain.Privacy;

/// <summary>
/// 账号脱敏：<c>SHA256(account)</c> 十六进制前 16 位，对应上游
/// <c>core/security.py::desensitize_account</c>。
/// </summary>
/// <remarks>
/// 统计里只存脱敏值，日志里也只用它，保证不会把真实账号写进磁盘。
/// 相同账号必须得到相同结果，因此不能用随机盐。
/// </remarks>
public static class AccountHasher
{
    /// <summary>计算账号的脱敏标识（小写十六进制，长度 16）。</summary>
    public static string Redact(string account)
    {
        ArgumentNullException.ThrowIfNull(account);

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(account));
        return Convert.ToHexStringLower(hash)[..16];
    }
}

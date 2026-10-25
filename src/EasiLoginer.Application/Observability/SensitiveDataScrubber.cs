using System.Text.RegularExpressions;

namespace EasiLoginer.Application.Observability;

/// <summary>
/// 遥测脱敏：把可能出现在异常消息/日志里的敏感片段打码。
/// </summary>
/// <remarks>
/// 纯函数，便于单测；Host 在 Sentry 的 <c>BeforeSend</c> 与日志输出前调用。
/// <list type="bullet">
///   <item><c>ea$</c> 开头的 Fernet 密文（账号/密码/主密码校验）</item>
///   <item>11 位手机号（希沃账号通常就是手机号）</item>
/// </list>
/// </remarks>
public static partial class SensitiveDataScrubber
{
    /// <summary>密文占位符。</summary>
    public const string SecretMask = "ea$***";

    /// <summary>手机号占位符。</summary>
    public const string PhoneMask = "1*********";

    [GeneratedRegex(@"ea\$[A-Za-z0-9_\-=]+", RegexOptions.CultureInvariant)]
    private static partial Regex SecretTokenPattern { get; }

    [GeneratedRegex(@"(?<!\d)1[3-9]\d{9}(?!\d)", RegexOptions.CultureInvariant)]
    private static partial Regex PhoneNumberPattern { get; }

    /// <summary>对文本做脱敏；<see langword="null"/> 原样返回。</summary>
    public static string? Scrub(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        var scrubbed = SecretTokenPattern.Replace(text, SecretMask);
        return PhoneNumberPattern.Replace(scrubbed, PhoneMask);
    }
}

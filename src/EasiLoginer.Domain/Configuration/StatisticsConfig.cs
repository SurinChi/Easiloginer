using System.Text.Json.Serialization;
using EasiLoginer.Domain.Automation;
using EasiLoginer.Domain.Privacy;

namespace EasiLoginer.Domain.Configuration;

/// <summary>统计数据，对应上游 <c>StatisticsConfig</c>。</summary>
/// <remarks>
/// 失败次数不单独计数，由总数减去成功与中断推出；上游把它作为计算字段
/// 一并写入 JSON，因此这里同样输出、但读取时忽略。
/// 按账号的计数只保存脱敏哈希，绝不落盘真实账号。
/// </remarks>
public sealed class StatisticsConfig
{
    /// <summary>首次运行时间。</summary>
    [JsonPropertyName("FirstRunDate")]
    public DateTimeOffset FirstRunDate { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>登录流程总数。</summary>
    [JsonPropertyName("LoginCounts")]
    public int LoginCounts { get; set; }

    /// <summary>登录成功数。</summary>
    [JsonPropertyName("LoginSuccessCounts")]
    public int LoginSuccessCounts { get; set; }

    /// <summary>被中断数。</summary>
    [JsonPropertyName("LoginInterruptCounts")]
    public int LoginInterruptCounts { get; set; }

    /// <summary>失败次数（计算属性，写出但不读取）。</summary>
    [JsonPropertyName("LoginFailureCounts")]
    public int LoginFailureCounts => LoginCounts - LoginSuccessCounts - LoginInterruptCounts;

    /// <summary>累计运行时长（秒）。</summary>
    [JsonPropertyName("TotalRunTime")]
    public double TotalRunTime { get; set; }

    /// <summary>累计登录时长（秒）。</summary>
    [JsonPropertyName("TotalLoginTime")]
    public double TotalLoginTime { get; set; }

    /// <summary>单次登录最长耗时（秒）。</summary>
    [JsonPropertyName("MaxLoginTime")]
    public double MaxLoginTime { get; set; }

    /// <summary>按脱敏账号统计的登录次数（键为账号哈希前 16 位）。</summary>
    [JsonPropertyName("LoginCountsPerAccount")]
    public Dictionary<string, int> LoginCountsPerAccount { get; set; } = [];

    /// <summary>本实例启动时间，不落盘（上游 <c>exclude=True</c>）。</summary>
    [JsonIgnore]
    public DateTimeOffset ThisInstanceLaunchTime { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>记录一次登录流程的开始（含按账号的脱敏计数）。</summary>
    public void RecordStart(string account)
    {
        ArgumentNullException.ThrowIfNull(account);

        LoginCounts++;

        var key = AccountHasher.Redact(account);
        LoginCountsPerAccount[key] = LoginCountsPerAccount.TryGetValue(key, out var count) ? count + 1 : 1;
    }

    /// <summary>记录一次登录流程的最终结果与耗时。</summary>
    /// <remarks>失败不单独计数，由 <see cref="LoginFailureCounts"/> 推出。</remarks>
    public void RecordResult(LoginOutcome outcome, double elapsedSeconds)
    {
        switch (outcome)
        {
            case LoginOutcome.Success:
                LoginSuccessCounts++;
                break;
            case LoginOutcome.Cancelled:
                LoginInterruptCounts++;
                break;
            case LoginOutcome.Failed:
            default:
                break;
        }

        TotalLoginTime += elapsedSeconds;
        MaxLoginTime = Math.Max(MaxLoginTime, elapsedSeconds);
    }

    /// <summary>
    /// 把遗留的明文账号键迁移为脱敏哈希，避免配置落盘泄露真实账号
    /// （对应上游 <c>_desensitize_account_keys</c>）。
    /// </summary>
    public void NormalizeAccountKeys()
    {
        if (LoginCountsPerAccount.Count == 0)
        {
            return;
        }

        var normalized = new Dictionary<string, int>(LoginCountsPerAccount.Count, StringComparer.Ordinal);
        foreach (var (key, count) in LoginCountsPerAccount)
        {
            var target = IsRedactedKey(key) ? key : AccountHasher.Redact(key);
            normalized[target] = normalized.TryGetValue(target, out var existing) ? existing + count : count;
        }

        LoginCountsPerAccount = normalized;
    }

    private static bool IsRedactedKey(string key)
    {
        if (key.Length != 16)
        {
            return false;
        }

        foreach (var character in key)
        {
            var isHex = character is >= '0' and <= '9' or >= 'a' and <= 'f';
            if (!isHex)
            {
                return false;
            }
        }

        return true;
    }
}

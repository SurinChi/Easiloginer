namespace EasiLoginer.Domain.Retention;

/// <summary>
/// 数据保留策略：应用长期未启动时清空全部数据，恢复初始状态。
/// </summary>
/// <remarks>
/// <list type="bullet">
///   <item>0 表示关闭该功能；</item>
///   <item>启用时最短 <see cref="MinimumDays"/> 天，防止误配置成"启动即清空"；</item>
///   <item>默认 <see cref="DefaultDays"/> 天（3 个月）。</item>
/// </list>
/// 纯函数，便于单测；实际文件操作在基础设施层。
/// </remarks>
public static class DataRetentionPolicy
{
    /// <summary>启用时的最短天数。</summary>
    public const int MinimumDays = 7;

    /// <summary>默认天数（约 3 个月）。</summary>
    public const int DefaultDays = 90;

    /// <summary>把配置值归一化为有效天数；0 表示禁用。</summary>
    public static int Normalize(int configuredDays)
        => configuredDays <= 0 ? 0 : Math.Max(MinimumDays, configuredDays);

    /// <summary>判断是否应当清空数据。</summary>
    /// <param name="lastLaunchUtc">上次启动时间（UTC）；未知时传 <see langword="null"/>。</param>
    /// <param name="nowUtc">当前时间（UTC）。</param>
    /// <param name="configuredDays">配置的天数（0 表示禁用）。</param>
    public static bool ShouldReset(DateTimeOffset? lastLaunchUtc, DateTimeOffset nowUtc, int configuredDays)
    {
        var days = Normalize(configuredDays);
        if (days == 0 || lastLaunchUtc is null)
        {
            return false;
        }

        return nowUtc - lastLaunchUtc.Value >= TimeSpan.FromDays(days);
    }
}

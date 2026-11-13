using EasiLoginer.Domain.Versioning;

namespace EasiLoginer.Domain.Announcements;

/// <summary>
/// 公告的展示规则（文档 §6.6）：时间窗口、版本范围、已隐藏 ID、排序与条数上限。
/// </summary>
/// <remarks>
/// 规则集中在这里而不是解析器或界面里，这样「哪条公告该出现」可以被单测直接验证，
/// 也保证本地文件与远程清单走完全相同的判定。
/// </remarks>
public static class AnnouncementPolicy
{
    /// <summary>一次最多展示多少条公告。</summary>
    public const int MaxVisible = 3;

    /// <summary>公告在指定时间是否处于展示窗口内。</summary>
    public static bool IsInWindow(Announcement announcement, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(announcement);

        return (announcement.StartAt is null || announcement.StartAt <= now) &&
               (announcement.EndAt is null || now < announcement.EndAt);
    }

    /// <summary>
    /// 当前版本是否落在公告的适用版本范围内。
    /// </summary>
    /// <remarks>
    /// 版本边界为空或无法解析时，按「不限制」处理——一条公告写错版本号不应该让它彻底消失，
    /// 也不应该把它错误地展示给所有人；这里选择后者（忽略该边界）。
    /// </remarks>
    public static bool MatchesVersion(Announcement announcement, Pep440Version currentVersion)
    {
        ArgumentNullException.ThrowIfNull(announcement);
        ArgumentNullException.ThrowIfNull(currentVersion);

        if (Pep440Version.TryParse(announcement.MinVersion, out var minimum) &&
            currentVersion.CompareTo(minimum) < 0)
        {
            return false;
        }

        return !Pep440Version.TryParse(announcement.MaxVersion, out var maximum) ||
               currentVersion.CompareTo(maximum) < 0;
    }

    /// <summary>按规则筛选并排序公告。</summary>
    /// <param name="announcements">全部候选公告。</param>
    /// <param name="currentVersion">当前应用版本。</param>
    /// <param name="now">当前时间。</param>
    /// <param name="hiddenIds">已隐藏的公告 ID（持久化在配置里）。</param>
    /// <param name="max">最多返回条数。</param>
    public static IReadOnlyList<Announcement> Select(
        IEnumerable<Announcement> announcements,
        Pep440Version currentVersion,
        DateTimeOffset now,
        IReadOnlyCollection<string>? hiddenIds = null,
        int max = MaxVisible)
    {
        ArgumentNullException.ThrowIfNull(announcements);
        ArgumentNullException.ThrowIfNull(currentVersion);

        var hidden = hiddenIds is null
            ? new HashSet<string>(StringComparer.Ordinal)
            : new HashSet<string>(hiddenIds, StringComparer.Ordinal);

        return announcements
            .Where(announcement => !string.IsNullOrWhiteSpace(announcement.Id))
            .Where(announcement => !hidden.Contains(announcement.Id))
            .Where(announcement => IsInWindow(announcement, now) && MatchesVersion(announcement, currentVersion))
            .OrderByDescending(announcement => announcement.PublishedAt)
            .ThenBy(announcement => announcement.Id, StringComparer.Ordinal)
            .Take(Math.Max(0, max))
            .ToList();
    }
}

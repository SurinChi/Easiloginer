namespace EasiLoginer.Domain.Announcements;

/// <summary>公告的严重程度；只影响界面配色，不改变过滤规则。</summary>
public enum AnnouncementSeverity
{
    /// <summary>普通信息。</summary>
    Info,

    /// <summary>需要注意。</summary>
    Warning,

    /// <summary>重要提示（例如需要立即升级）。</summary>
    Critical,
}

/// <summary>
/// 一条应用内公告（文档 §6.6）。
/// </summary>
/// <param name="Id">稳定标识；用于持久化「已隐藏」，发布后不要修改。</param>
/// <param name="Title">标题。</param>
/// <param name="Body">正文（纯文本，换行按原样显示）。</param>
/// <param name="Severity">严重程度。</param>
/// <param name="PublishedAt">发布时间，用于排序（新→旧）。</param>
/// <param name="StartAt">开始展示时间；为空表示立即生效。</param>
/// <param name="EndAt">结束展示时间（不含）；为空表示长期有效。</param>
/// <param name="MinVersion">最低适用版本（含）；无法解析时忽略该限制。</param>
/// <param name="MaxVersion">最高适用版本（不含）；无法解析时忽略该限制。</param>
/// <param name="Url">可选的详情链接（仅 http/https）。</param>
/// <param name="Dismissible">是否允许用户勾选「不再提示」。</param>
public sealed record Announcement(
    string Id,
    string Title,
    string Body,
    AnnouncementSeverity Severity,
    DateTimeOffset PublishedAt,
    DateTimeOffset? StartAt = null,
    DateTimeOffset? EndAt = null,
    string? MinVersion = null,
    string? MaxVersion = null,
    string? Url = null,
    bool Dismissible = true);

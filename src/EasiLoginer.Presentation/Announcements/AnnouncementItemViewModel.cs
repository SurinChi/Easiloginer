using System.Windows.Media;
using EasiLoginer.Domain.Announcements;

namespace EasiLoginer.Presentation.Announcements;

/// <summary>公告窗口里一条公告的显示数据（颜色由严重程度决定）。</summary>
public sealed class AnnouncementItemViewModel
{
    /// <summary>创建显示数据。</summary>
    public AnnouncementItemViewModel(Announcement announcement)
    {
        ArgumentNullException.ThrowIfNull(announcement);

        Title = announcement.Title;
        Body = announcement.Body;
        Url = announcement.Url;
        Accent = CreateAccent(announcement.Severity);
    }

    /// <summary>标题。</summary>
    public string Title { get; }

    /// <summary>正文。</summary>
    public string Body { get; }

    /// <summary>详情链接（可能为空）。</summary>
    public string? Url { get; }

    /// <summary>是否有详情链接。</summary>
    public bool HasLink => !string.IsNullOrEmpty(Url);

    /// <summary>左侧色条 / 标题强调色。</summary>
    public Brush Accent { get; }

    private static SolidColorBrush CreateAccent(AnnouncementSeverity severity)
    {
        var color = severity switch
        {
            AnnouncementSeverity.Critical => Color.FromRgb(0xD1, 0x34, 0x38),
            AnnouncementSeverity.Warning => Color.FromRgb(0xF7, 0x9B, 0x00),
            _ => Color.FromRgb(0x00, 0x78, 0xD4),
        };

        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}

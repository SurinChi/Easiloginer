using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using Wpf.Ui.Controls;
using EasiLoginer.Domain.Announcements;

namespace EasiLoginer.Presentation.Announcements;

/// <summary>
/// 应用内公告窗口：一次最多显示 3 条（由 <see cref="AnnouncementPolicy"/> 过滤）。
/// </summary>
public partial class AnnouncementWindow : FluentWindow
{
    private readonly IReadOnlyList<Announcement> _announcements;

    /// <summary>创建公告窗口。</summary>
    public AnnouncementWindow(IReadOnlyList<Announcement> announcements)
    {
        ArgumentNullException.ThrowIfNull(announcements);

        _announcements = announcements;
        InitializeComponent();

        ItemsHost.ItemsSource = announcements.Select(item => new AnnouncementItemViewModel(item)).ToList();
        DontShowAgain.IsEnabled = announcements.Any(item => item.Dismissible);
    }

    /// <summary>用户选择「不再提示」时需要持久化的公告 ID。</summary>
    public IReadOnlyList<string> HiddenIds { get; private set; } = [];

    /// <inheritdoc />
    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);

        // 关闭窗口（包括 Alt+F4）时按复选框的当前状态收尾
        if (DontShowAgain.IsChecked == true)
        {
            HiddenIds = _announcements
                .Where(item => item.Dismissible)
                .Select(item => item.Id)
                .ToList();
        }
    }

    private void OnCloseClicked(object sender, RoutedEventArgs e) => Close();

    private void OnOpenLinkClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: string url } ||
            !Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
        {
            // 没有默认浏览器时忽略：公告的详情链接不是核心功能
        }
    }
}

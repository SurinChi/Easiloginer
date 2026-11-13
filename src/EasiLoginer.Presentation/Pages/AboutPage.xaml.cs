using System.Windows;
using System.Windows.Controls;
using EasiLoginer.Presentation.Announcements;

namespace EasiLoginer.Presentation.Pages;

/// <summary>关于页：版本、公告入口、上游归属与许可信息（文档 §10.2）。</summary>
public partial class AboutPage : Page
{
    private readonly AppServices _services;

    /// <summary>创建关于页。</summary>
    public AboutPage(AppServices services)
    {
        ArgumentNullException.ThrowIfNull(services);

        _services = services;
        InitializeComponent();

        // 页面高度必须钉在导航容器内，否则内部 ScrollViewer 会被撑成内容高度，
        // 滚轮事件落不到它身上（关于页正文较长，需要能滚动）。
        PageLayout.ConstrainToNavigationHost(this);

        VersionText.Text = $"版本 {AppVersion.Text} · GPLv3";
    }

    private async void OnAnnouncementsClicked(object sender, RoutedEventArgs e)
    {
        AnnouncementButton.IsEnabled = false;

        try
        {
            await AnnouncementCenter.ShowAsync(_services, Window.GetWindow(this));
            AnnouncementStatus.Text = "已是最新公告。";
        }
        finally
        {
            AnnouncementButton.IsEnabled = true;
        }
    }
}

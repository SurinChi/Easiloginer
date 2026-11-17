using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using EasiLoginer.Presentation.Announcements;

namespace EasiLoginer.Presentation.Pages;

/// <summary>公告页：版本更新与维护通知的常驻入口（导航位于「关于」下方）。</summary>
public partial class AnnouncementPage : Page
{
    private readonly AppServices _services;
    private bool _loaded;

    /// <summary>创建公告页。</summary>
    public AnnouncementPage(AppServices services)
    {
        ArgumentNullException.ThrowIfNull(services);

        _services = services;
        InitializeComponent();
        PageLayout.ConstrainToNavigationHost(this);
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_loaded)
        {
            return;
        }

        _loaded = true;
        await RefreshAsync();
    }

    private async void OnRefreshClicked(object sender, RoutedEventArgs e) => await RefreshAsync();

    private async Task RefreshAsync()
    {
        RefreshButton.IsEnabled = false;
        StatusText.Text = "正在获取公告…";

        try
        {
            var announcements = await AnnouncementCenter.FetchAsync(
                _services,
                includeHidden: false,
                CancellationToken.None);

            ItemsHost.ItemsSource = announcements
                .Select(announcement => new AnnouncementItemViewModel(announcement))
                .ToList();

            var hasItems = announcements.Count > 0;
            EmptyText.Visibility = hasItems ? Visibility.Collapsed : Visibility.Visible;
            StatusText.Text = hasItems
                ? $"共 {announcements.Count} 条公告。"
                : "暂无公告。";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            ItemsHost.ItemsSource = null;
            EmptyText.Visibility = Visibility.Visible;
            StatusText.Text = $"获取公告失败：{exception.Message}";
        }
        finally
        {
            RefreshButton.IsEnabled = true;
        }
    }

    private void OnOpenLinkClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string url } ||
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

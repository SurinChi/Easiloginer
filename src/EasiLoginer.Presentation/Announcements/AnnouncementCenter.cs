using System.IO;
using System.Windows;
using EasiLoginer.Domain.Announcements;

namespace EasiLoginer.Presentation.Announcements;

/// <summary>
/// 公告的展示入口：启动时自动检查（有频率上限），公告页可手动刷新。
/// </summary>
/// <remarks>
/// 公告是「有则更好」的能力：任何失败都不弹错误、不阻塞启动（文档 §6.6）。
/// 网络与清单错误由 <see cref="Application.Announcements.AnnouncementService"/> 记日志并降级为空列表，
/// 这里只保证界面层的意外（例如窗口创建失败）不会变成未观察的任务异常。
/// </remarks>
internal static class AnnouncementCenter
{
    /// <summary>自动检查的最小间隔。</summary>
    private static readonly TimeSpan AutoCheckInterval = TimeSpan.FromHours(6);

    /// <summary>启动时检查一次公告；超过间隔才真正发起读取。</summary>
    public static async Task AutoCheckAsync(AppServices services, Window? owner)
    {
        ArgumentNullException.ThrowIfNull(services);

        try
        {
            var config = services.Config.Current;

            // 首次向导还没结束：不要在向导后面再叠一个公告窗口
            if (!config.Internal.IsOobeCompleted)
            {
                return;
            }

            var now = DateTimeOffset.Now;
            if (config.Internal.LastAnnouncementCheckTime is { } last && now - last < AutoCheckInterval)
            {
                return;
            }

            config.Internal.LastAnnouncementCheckTime = now;
            services.Config.Save();

            await ShowCoreAsync(services, owner).ConfigureAwait(true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // 写配置失败也不影响主界面：下次启动再试
        }
    }

    /// <summary>
    /// 读取公告列表，供公告页使用；<paramref name="includeHidden"/> 为 false 时
    /// 忽略「不再提示」记录，让用户仍能在页面里主动查看全部公告。
    /// </summary>
    internal static async Task<IReadOnlyList<Announcement>> FetchAsync(
        AppServices services,
        bool includeHidden,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);

        var version = AppVersion.TryGet();
        if (version is null)
        {
            return [];
        }

        var hidden = includeHidden
            ? services.Config.Current.Internal.HiddenAnnouncementIds
            : null;

        return await services.Announcements
            .FetchAsync(version, hidden, cancellationToken)
            .ConfigureAwait(true);
    }

    private static async Task ShowCoreAsync(AppServices services, Window? owner)
    {
        var announcements = await FetchAsync(services, includeHidden: true).ConfigureAwait(true);
        if (announcements.Count == 0)
        {
            return;
        }

        var window = new AnnouncementWindow(announcements);

        if (owner is { IsVisible: true })
        {
            window.Owner = owner;
        }

        // 与其它独立窗口一致：显式套用一次主题，避免深浅色错配
        Wpf.Ui.Appearance.ApplicationThemeManager.Apply(window);
        window.ShowDialog();

        SaveHiddenIds(
            services,
            services.Config.Current.Internal.HiddenAnnouncementIds,
            window.HiddenIds);
    }

    private static void SaveHiddenIds(
        AppServices services,
        List<string> hidden,
        IReadOnlyList<string> newlyHidden)
    {
        if (newlyHidden.Count == 0)
        {
            return;
        }

        var changed = false;
        foreach (var id in newlyHidden)
        {
            if (!hidden.Contains(id))
            {
                hidden.Add(id);
                changed = true;
            }
        }

        if (changed)
        {
            services.Config.Save();
        }
    }
}

using EasiLoginer.Application.Abstractions;
using EasiLoginer.Application.Automation;
using EasiLoginer.Domain.Automation;
using EasiLoginer.Domain.Profiles;
using EasiLoginer.Presentation.Overlays;

namespace EasiLoginer.Presentation.Login;

/// <summary>
/// 按档案执行登录：组装遮罩/横幅与确认弹窗，供档案页和快捷登录页共用。
/// </summary>
internal static class ProfileLoginRunner
{
    /// <summary>运行指定档案的登录流程。</summary>
    public static async Task<LoginOutcome> RunAsync(
        AutomationProfile profile,
        ILoginRunner login,
        IConfigService config,
        IDisplayMetrics display,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(profile);

        using var cancelSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var scale = display.Capture().Scale;
        var presenter = new WpfLoginPresenter(
            display,
            BannerOptions.FromConfig(config.Current.Banner, scale),
            config.Current.StatusOverlay.Enabled,
            cancelRequested: () =>
            {
                try
                {
                    cancelSource.Cancel();
                }
                catch (ObjectDisposedException)
                {
                    // 登录已经结束、浮窗正在淡出：忽略迟到的点击
                }
            });
        var options = new LoginRunOptions(presenter, new WpfLoginConfirmation());

        return await login
            .RunForProfileAsync(profile.Id, options, cancelSource.Token)
            .ConfigureAwait(true);
    }
}

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

        var scale = display.Capture().Scale;
        var presenter = new WpfLoginPresenter(
            display,
            BannerOptions.FromConfig(config.Current.Banner, scale),
            config.Current.StatusOverlay.Enabled);
        var options = new LoginRunOptions(presenter, new WpfLoginConfirmation());

        return await login
            .RunForProfileAsync(profile.Id, options, cancellationToken)
            .ConfigureAwait(true);
    }
}

using System.Windows.Threading;
using EasiLoginer.Application.Abstractions;
using EasiLoginer.Application.Automation;
using EasiLoginer.Domain.Automation;
using EasiLoginer.Presentation.Overlays;

namespace EasiLoginer.Presentation.Login;

/// <summary>
/// WPF 版登录呈现：隐私遮罩 + 状态浮窗。
/// </summary>
/// <remarks>
/// 登录管线可能在线程池线程上回调，因此所有窗口操作都通过 UI <see cref="Dispatcher"/> 执行。
/// 遮罩坐标为物理像素，这里按主屏缩放换算成 DIP（文档 §10.5）。
/// </remarks>
public sealed class WpfLoginPresenter : ILoginPresenter
{
    private static readonly TimeSpan OverlayLingerTime = TimeSpan.FromSeconds(3);

    private readonly Dispatcher _dispatcher;
    private readonly double _scale;
    private readonly BannerOptions? _bannerOptions;
    private PrivacyMaskWindow? _mask;
    private StatusOverlayWindow? _overlay;
    private BannerWindow? _banner;

    /// <summary>在 UI 线程创建呈现器。</summary>
    public WpfLoginPresenter(IDisplayMetrics display, BannerOptions? bannerOptions = null)
    {
        ArgumentNullException.ThrowIfNull(display);

        _dispatcher = Dispatcher.CurrentDispatcher;
        _scale = display.Capture().Scale is > 0 and var scale ? scale : 1.0;
        _bannerOptions = bannerOptions;
    }

    /// <inheritdoc />
    public void OnTaskChanged(string task) => Post(() => EnsureOverlay().SetTask(task));

    /// <inheritdoc />
    public void OnProgressChanged(string progress) => Post(() => EnsureOverlay().SetProgress(progress));

    /// <inheritdoc />
    public void ShowPrivacyMask(int x, int y, int width, int height) => Post(() =>
    {
        var rect = PrivacyMaskGeometry.ToDips(x, y, width, height, _scale);
        _mask ??= new PrivacyMaskWindow();
        _mask.ShowAt(rect.Left, rect.Top, rect.Width, rect.Height);
    });

    /// <inheritdoc />
    public void HidePrivacyMask() => Post(() =>
    {
        _mask?.Close();
        _mask = null;
    });

    /// <inheritdoc />
    public void ShowBanner() => Post(() =>
    {
        if (_bannerOptions is null)
        {
            return;
        }

        _banner ??= new BannerWindow(_bannerOptions);
        _banner.Show();
    });

    /// <inheritdoc />
    public void HideBanner() => Post(() =>
    {
        _banner?.Close();
        _banner = null;
    });

    /// <inheritdoc />
    public void OnFinished(LoginOutcome outcome, string? error) => Post(() =>
    {
        var overlay = EnsureOverlay();
        overlay.SetFinal(outcome, error);

        var timer = new DispatcherTimer { Interval = OverlayLingerTime };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            overlay.Close();

            if (ReferenceEquals(_overlay, overlay))
            {
                _overlay = null;
            }
        };
        timer.Start();
    });

    private StatusOverlayWindow EnsureOverlay()
    {
        if (_overlay is null)
        {
            _overlay = new StatusOverlayWindow();
            _overlay.Show();
        }

        return _overlay;
    }

    private void Post(Action action)
    {
        if (_dispatcher.CheckAccess())
        {
            action();
            return;
        }

        _dispatcher.BeginInvoke(action);
    }
}

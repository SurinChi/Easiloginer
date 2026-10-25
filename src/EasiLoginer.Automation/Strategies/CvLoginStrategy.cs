using EasiLoginer.Application.Abstractions;
using EasiLoginer.Application.Automation;
using EasiLoginer.Automation.Cv;
using EasiLoginer.Automation.Input;
using EasiLoginer.Domain.Automation;
using EasiLoginer.Domain.Geometry;

namespace EasiLoginer.Automation.Strategies;

/// <summary>
/// 图像识别策略，对应上游 <c>automator/cv.py</c>。
/// </summary>
/// <remarks>
/// <para>仅支持 1920×1080@100% 与 3840×2160@200% 两种显示环境，其他环境在
/// <see cref="BeforePrepareAsync"/> 阶段直接失败（不可重试），与上游一致。</para>
/// <para>流程：进入登录界面（IWB）→ 识别账号登录按钮并切换 → 账号 → 密码 →
/// 识别协议勾选框 → 回车提交。上游在识别失败时会用同一个模板重试（无意义），
/// 这里改为识别“已选中”样式，属于文档 §9.4 允许的修正。</para>
/// </remarks>
public sealed class CvLoginStrategy : ILoginStrategy
{
    private readonly IInputSimulator _input;
    private readonly ITemplateLocator _locator;
    private readonly IEasiNoteSession _session;
    private readonly IDisplayMetrics _display;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;

    private string _variant = string.Empty;
    private bool _isIwb;

    /// <summary>创建策略。</summary>
    public CvLoginStrategy(
        IInputSimulator input,
        ITemplateLocator locator,
        IEasiNoteSession session,
        IDisplayMetrics display,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(locator);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(display);

        _input = input;
        _locator = locator;
        _session = session;
        _display = display;
        _delay = delay ?? ((duration, token) => Task.Delay(duration, token));
    }

    /// <inheritdoc />
    public string Id => "cv";

    /// <inheritdoc />
    public string ResolveLaunchArgs(LoginContext context) => context.Config.Login.EasiNote.Args;

    /// <inheritdoc />
    public Task BeforePrepareAsync(LoginContext context, CancellationToken cancellationToken)
    {
        var screen = context.Screen ?? _display.Capture();
        context.Screen = screen;

        var variant = ImageTemplateVariant.Resolve(screen.PhysicalSize, screen.Scale);
        if (variant is null)
        {
            throw new LoginException(
                $"当前显示环境 {ImageTemplateVariant.Describe(screen.PhysicalSize, screen.Scale)} 不支持图像识别，" +
                $"仅支持 {ImageTemplateVariant.SupportedDescription}",
                canRetry: false);
        }

        _variant = variant;
        _isIwb = _session.ResolveIsIwb(context.Config.Login.EasiNote.Args);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task LoginAsync(LoginContext context, CancellationToken cancellationToken)
    {
        var screen = context.Screen ?? _display.Capture();
        var scale = screen.Scale;
        var suffix = (_isIwb ? string.Empty : "_direct") + _variant;

        // 进入登录界面（仅 IWB 需要）
        if (_isIwb)
        {
            cancellationToken.ThrowIfCancellationRequested();
            context.ReportProgress("进入登录界面");

            await _input.ClickAsync((int)(172 * scale), (int)(1044 * scale), cancellationToken).ConfigureAwait(false);
            await _delay(TimeSpan.FromSeconds(context.Config.Login.Timeout.EnterLoginUI), cancellationToken)
                .ConfigureAwait(false);
        }

        // 切换至账号登录页：优先普通按钮，失败再看“已选中”样式
        cancellationToken.ThrowIfCancellationRequested();
        context.ReportProgress("切换至账号登录页");

        var found = _locator.Locate("account_login_button" + suffix)
                    ?? _locator.Locate("account_login_button_selected" + suffix);

        if (found is null)
        {
            throw new LoginException("未识别到账号登录按钮", canRetry: true);
        }

        var accountButton = found.Value;

        await _input.ClickAsync(accountButton.X, accountButton.Y, cancellationToken).ConfigureAwait(false);
        await _delay(TimeSpan.FromSeconds(context.Config.Login.Timeout.SwitchTab), cancellationToken)
            .ConfigureAwait(false);

        // 隐私遮罩（与固定位置策略一致：位置按换算、尺寸按缩放）
        var mask = context.Config.Experimental.PrivacyMask;
        if (mask.Enabled)
        {
            var position = context.Config.Login.Position;
            var maskPosition = position.EnableScaling
                ? LoginWindowGeometry.ToScreen(
                    mask.MaskLeftTop,
                    position.LoginWindowSize,
                    position.BaseSize,
                    screen.PhysicalSize,
                    scale)
                : mask.MaskLeftTop;

            var maskSize = new PixelPoint((int)(mask.MaskSize.X * scale), (int)(mask.MaskSize.Y * scale));
            context.Presenter.ShowPrivacyMask(maskPosition.X, maskPosition.Y, maskSize.X, maskSize.Y);
        }

        // 输入账号
        cancellationToken.ThrowIfCancellationRequested();
        context.ReportProgress("输入账号");

        await _input.ClickAsync(
                accountButton.X,
                (int)(accountButton.Y + (70 * scale)),
                cancellationToken)
            .ConfigureAwait(false);
        await _input.TypeTextAsync(context.Account, secret: false, cancellationToken).ConfigureAwait(false);

        // 输入密码
        cancellationToken.ThrowIfCancellationRequested();
        context.ReportProgress("输入密码");

        await _input.ClickAsync(
                accountButton.X,
                (int)(accountButton.Y + (134 * scale)),
                cancellationToken)
            .ConfigureAwait(false);
        await _input.TypeTextAsync(context.Password, secret: true, cancellationToken).ConfigureAwait(false);

        // 勾选同意用户协议
        cancellationToken.ThrowIfCancellationRequested();
        context.ReportProgress("勾选同意用户协议");

        var agreement = _locator.Locate("agreement_checkbox" + suffix)
                        ?? throw new LoginException("未识别到用户协议勾选框", canRetry: true);

        await _input.ClickAsync(agreement.X, agreement.Y, cancellationToken).ConfigureAwait(false);

        // 提交
        cancellationToken.ThrowIfCancellationRequested();
        context.ReportProgress("点击登录按钮");

        await _input.PressEnterAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task OnFinishedAsync(LoginContext context, LoginOutcome outcome, string? error, CancellationToken cancellationToken)
    {
        context.Presenter.HidePrivacyMask();
        return Task.CompletedTask;
    }
}

using EasiLoginer.Application.Automation;
using EasiLoginer.Automation.Input;
using EasiLoginer.Domain.Automation;
using EasiLoginer.Domain.Geometry;

namespace EasiLoginer.Automation.Strategies;

/// <summary>
/// 固定位置策略，对应上游 <c>automator/fixed.py</c>。
/// </summary>
/// <remarks>
/// <para>坐标换算遵循 <see cref="LoginWindowGeometry"/>：窗口内坐标走相对换算，
/// 只有「进入登录界面」按钮使用左下角独立公式；<c>EnableScaling=false</c> 时整条链路不缩放。</para>
/// <para>步骤顺序与上游一致：进入登录界面 →（隐私遮罩）→ 切账号页 → 账号 → 密码 → 同意协议 → 回车提交。</para>
/// </remarks>
public sealed class FixedPositionStrategy : ILoginStrategy
{
    private readonly IInputSimulator _input;
    private readonly IEasiNoteSession _session;
    private readonly IDisplayMetrics _display;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;

    /// <summary>创建策略。</summary>
    public FixedPositionStrategy(
        IInputSimulator input,
        IEasiNoteSession session,
        IDisplayMetrics display,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(display);

        _input = input;
        _session = session;
        _display = display;
        _delay = delay ?? ((duration, token) => Task.Delay(duration, token));
    }

    /// <inheritdoc />
    public string Id => "fixed";

    /// <inheritdoc />
    public string ResolveLaunchArgs(LoginContext context)
    {
        var args = context.Config.Login.EasiNote.Args;
        var mode = LaunchModeParser.Parse(args);

        // 固定位置的坐标基于白板界面；只有确实会进入该界面时才沿用用户参数
        if (string.Equals(mode, "display", StringComparison.Ordinal) ||
            (mode is null && _session.ResolveIsIwb(args)))
        {
            return args;
        }

        return "-m Display iwb";
    }

    /// <inheritdoc />
    public Task BeforePrepareAsync(LoginContext context, CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public async Task LoginAsync(LoginContext context, CancellationToken cancellationToken)
    {
        var position = context.Config.Login.Position;
        var screen = context.Screen ?? _display.Capture();
        context.Screen = screen;

        var physical = screen.PhysicalSize;
        var scale = screen.Scale;
        var scaling = position.EnableScaling;

        PixelPoint Resolve(PixelPoint point) => scaling
            ? LoginWindowGeometry.ToScreen(point, position.LoginWindowSize, position.BaseSize, physical, scale)
            : point;

        // 进入登录界面：唯一使用左下角独立公式的坐标
        cancellationToken.ThrowIfCancellationRequested();
        context.ReportProgress("进入登录界面");

        var enter = scaling
            ? LoginWindowGeometry.ToScreenEnterLogin(position.EnterLogin, position.BaseSize, physical, scale)
            : position.EnterLogin;

        await _input.ClickAsync(enter.X, enter.Y, cancellationToken).ConfigureAwait(false);
        await _delay(TimeSpan.FromSeconds(context.Config.Login.Timeout.EnterLoginUI), cancellationToken).ConfigureAwait(false);

        // 隐私遮罩（位置随缩放换算；尺寸始终按缩放缩放，与上游一致）
        var mask = context.Config.Experimental.PrivacyMask;
        if (mask.Enabled)
        {
            var maskPosition = Resolve(mask.MaskLeftTop);
            var maskSize = new PixelPoint((int)(mask.MaskSize.X * scale), (int)(mask.MaskSize.Y * scale));
            context.Presenter.ShowPrivacyMask(maskPosition.X, maskPosition.Y, maskSize.X, maskSize.Y);
        }

        // 切换至账号登录页
        cancellationToken.ThrowIfCancellationRequested();
        context.ReportProgress("切换至账号登录页");

        var tab = Resolve(position.AccountLoginTab);
        await _input.ClickAsync(tab.X, tab.Y, cancellationToken).ConfigureAwait(false);
        await _delay(TimeSpan.FromSeconds(context.Config.Login.Timeout.SwitchTab), cancellationToken).ConfigureAwait(false);

        // 输入账号
        cancellationToken.ThrowIfCancellationRequested();
        context.ReportProgress("输入账号");

        var account = Resolve(position.AccountInput);
        await _input.ClickAsync(account.X, account.Y, cancellationToken).ConfigureAwait(false);
        await _input.TypeTextAsync(context.Account, secret: false, cancellationToken).ConfigureAwait(false);

        // 输入密码
        cancellationToken.ThrowIfCancellationRequested();
        context.ReportProgress("输入密码");

        var password = Resolve(position.PasswordInput);
        await _input.ClickAsync(password.X, password.Y, cancellationToken).ConfigureAwait(false);
        await _input.TypeTextAsync(context.Password, secret: true, cancellationToken).ConfigureAwait(false);

        // 勾选同意用户协议
        cancellationToken.ThrowIfCancellationRequested();
        context.ReportProgress("勾选同意用户协议");

        var agreement = Resolve(position.AgreementCheckbox);
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

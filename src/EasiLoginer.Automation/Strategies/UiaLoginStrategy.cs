using EasiLoginer.Application.Automation;
using EasiLoginer.Application.Abstractions;
using EasiLoginer.Automation.Uia;
using EasiLoginer.Domain.Automation;

namespace EasiLoginer.Automation.Strategies;

/// <summary>
/// UI Automation 策略，对应上游 <c>automator/uia.py</c>。
/// </summary>
/// <remarks>
/// <para>流程：等待登录窗 →（IWB：点头像 → 等 <c>IWBLogin</c>）→ 切账号页 → 账号 → 密码 →
/// （隐私遮罩取二维码矩形）→ 勾协议 → 点登录。</para>
/// <para>元素找不到属于<b>可重试</b>失败（界面可能还没加载完）；与上游 §9.2 一致。</para>
/// <para>本策略不依赖 <c>vendors/</c> 里的管道桥，因此「已安装但未修补」的机器也能用。</para>
/// </remarks>
public sealed class UiaLoginStrategy : ILoginStrategy
{
    private static readonly TimeSpan ElementTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan IwBridgeProbe = TimeSpan.FromMilliseconds(400);
    private static readonly TimeSpan RadioProbe = TimeSpan.FromMilliseconds(600);
    private static readonly TimeSpan StepDelay = TimeSpan.FromMilliseconds(100);

    private readonly IEasiNoteUi _ui;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;

    /// <summary>创建策略。</summary>
    public UiaLoginStrategy(
        IEasiNoteUi ui,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        ArgumentNullException.ThrowIfNull(ui);

        _ui = ui;
        _delay = delay ?? ((duration, token) => Task.Delay(duration, token));
    }

    /// <inheritdoc />
    public string Id => "uia";

    /// <inheritdoc />
    public string ResolveLaunchArgs(LoginContext context) => context.Config.Login.EasiNote.Args;

    /// <inheritdoc />
    public Task BeforePrepareAsync(LoginContext context, CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public async Task LoginAsync(LoginContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        context.ReportProgress("等待希沃登录窗口");

        if (!await _ui.WaitForElementAsync(EasiNoteUiIds.AccountLoginRadio, ElementTimeout, cancellationToken)
                .ConfigureAwait(false)
            && !await _ui.WaitForElementAsync(EasiNoteUiIds.IwbProfileButton, IwBridgeProbe, cancellationToken)
                .ConfigureAwait(false)
            && !await _ui.WaitForElementAsync(EasiNoteUiIds.AccountInput, IwBridgeProbe, cancellationToken)
                .ConfigureAwait(false))
        {
            throw new LoginException("未找到希沃登录界面（UIA）", canRetry: true);
        }

        await EnterLoginIfNeededAsync(context, cancellationToken).ConfigureAwait(false);

        // 切换至账号登录页
        cancellationToken.ThrowIfCancellationRequested();
        context.ReportProgress("切换至账号登录页");

        var radioId = await _ui.WaitForElementAsync(EasiNoteUiIds.IwbAccountRadio, RadioProbe, cancellationToken)
            .ConfigureAwait(false)
            ? EasiNoteUiIds.IwbAccountRadio
            : EasiNoteUiIds.AccountLoginRadio;

        if (!await _ui.ClickAsync(radioId, cancellationToken).ConfigureAwait(false))
        {
            throw new LoginException($"无法点击账号登录页（{radioId}）", canRetry: true);
        }

        await _delay(StepDelay, cancellationToken).ConfigureAwait(false);

        // 输入账号
        cancellationToken.ThrowIfCancellationRequested();
        context.ReportProgress("输入账号");

        if (!await _ui.SetTextAsync(EasiNoteUiIds.AccountInput, context.Account, cancellationToken).ConfigureAwait(false))
        {
            throw new LoginException("未找到账号输入框（UserIdTextBox）", canRetry: true);
        }

        // 输入密码
        cancellationToken.ThrowIfCancellationRequested();
        context.ReportProgress("输入密码");

        if (!await _ui.SetTextAsync(EasiNoteUiIds.PasswordBox, context.Password, cancellationToken).ConfigureAwait(false))
        {
            throw new LoginException("未找到密码输入框（PasswordBox）", canRetry: true);
        }

        // 隐私遮罩：取二维码容器的矩形
        var mask = context.Config.Experimental.PrivacyMask;
        if (mask.Enabled)
        {
            var rect = await _ui.GetRectAsync(EasiNoteUiIds.QrCodeControl, cancellationToken).ConfigureAwait(false)
                       ?? await _ui.GetRectAsync(EasiNoteUiIds.IwbQrCodeControl, cancellationToken).ConfigureAwait(false);

            if (rect is { Width: > 0, Height: > 0 } area)
            {
                context.Presenter.ShowPrivacyMask(area.X, area.Y, area.Width, area.Height);
            }
        }

        // 勾选同意用户协议（已勾选则跳过）
        cancellationToken.ThrowIfCancellationRequested();
        context.ReportProgress("勾选同意用户协议");

        var toggled = await _ui.IsToggledAsync(EasiNoteUiIds.AgreementCheckBox, cancellationToken).ConfigureAwait(false);
        if (toggled == false
            && !await _ui.ClickAsync(EasiNoteUiIds.AgreementCheckBox, cancellationToken).ConfigureAwait(false))
        {
            throw new LoginException("未找到同意协议复选框（AgreementCheckBox）", canRetry: true);
        }

        // 提交
        cancellationToken.ThrowIfCancellationRequested();
        context.ReportProgress("点击登录按钮");

        if (!await _ui.ClickAsync(EasiNoteUiIds.LoginButton, cancellationToken).ConfigureAwait(false))
        {
            throw new LoginException("未找到登录按钮（LoginButton）", canRetry: true);
        }

        await _delay(StepDelay, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task OnFinishedAsync(LoginContext context, LoginOutcome outcome, string? error, CancellationToken cancellationToken)
    {
        context.Presenter.HidePrivacyMask();
        return Task.CompletedTask;
    }

    private async Task EnterLoginIfNeededAsync(LoginContext context, CancellationToken cancellationToken)
    {
        var iwb = await _ui.WaitForElementAsync(EasiNoteUiIds.IwbProfileButton, IwBridgeProbe, cancellationToken)
            .ConfigureAwait(false);

        if (!iwb)
        {
            return;
        }

        context.ReportProgress("进入登录界面");

        if (!await _ui.ClickAsync(EasiNoteUiIds.IwbProfileButton, cancellationToken).ConfigureAwait(false))
        {
            throw new LoginException("无法点击头像按钮（ProfileButton）", canRetry: true);
        }

        if (!await _ui.WaitForElementAsync(EasiNoteUiIds.IwbLoginWindow, ElementTimeout, cancellationToken)
                .ConfigureAwait(false)
            && !await _ui.WaitForElementAsync(EasiNoteUiIds.AccountInput, ElementTimeout, cancellationToken)
                .ConfigureAwait(false))
        {
            throw new LoginException("未出现登录窗口（IWBLogin）", canRetry: true);
        }
    }
}

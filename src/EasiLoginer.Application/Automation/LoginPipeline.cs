using System.Diagnostics;
using Microsoft.Extensions.Logging;
using EasiLoginer.Application.Abstractions;
using EasiLoginer.Domain.Automation;

namespace EasiLoginer.Application.Automation;

/// <summary>
/// 登录管线：准备 → 登录 → 收尾，含重试与取消语义。
/// </summary>
/// <remarks>
/// <para>逐项复刻上游 <c>BaseAutomator.run()</c>：</para>
/// <list type="bullet">
///   <item>先 <c>record_start</c>，再按 <c>App.MaxRetries</c> 重试；</item>
///   <item>不可重试错误（<see cref="LoginException.CanRetry"/> 为 false）立即结束；</item>
///   <item>每次重试前隐藏隐私遮罩，等待 2 秒；</item>
///   <item>取消（<see cref="OperationCanceledException"/>）映射为 <see cref="LoginOutcome.Cancelled"/>，不计失败；</item>
///   <item>收尾必定执行一次，且吞掉收尾回调自身的异常；</item>
///   <item>最后记录耗时与结果统计。</item>
/// </list>
/// </remarks>
public sealed class LoginPipeline
{
    private readonly IEasiNoteSession _session;
    private readonly ILoginUidCache _uidCache;
    private readonly ISeewoAuthenticator _authenticator;
    private readonly ILogger<LoginPipeline> _logger;
    private readonly TimeSpan _retryDelay;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;

    /// <summary>创建管线。</summary>
    /// <param name="session">希沃会话端口。</param>
    /// <param name="uidCache">档案中的 login_uid 缓存。</param>
    /// <param name="authenticator">希沃账号认证端口。</param>
    /// <param name="logger">日志。</param>
    /// <param name="retryDelay">重试间隔，默认 2 秒（与上游一致）。</param>
    /// <param name="delay">延时实现（测试可替换为立即返回）。</param>
    public LoginPipeline(
        IEasiNoteSession session,
        ILoginUidCache uidCache,
        ISeewoAuthenticator authenticator,
        ILogger<LoginPipeline> logger,
        TimeSpan? retryDelay = null,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(uidCache);
        ArgumentNullException.ThrowIfNull(authenticator);
        ArgumentNullException.ThrowIfNull(logger);

        _session = session;
        _uidCache = uidCache;
        _authenticator = authenticator;
        _logger = logger;
        _retryDelay = retryDelay ?? TimeSpan.FromSeconds(2);
        _delay = delay ?? ((duration, token) => Task.Delay(duration, token));
    }

    /// <summary>执行一次完整登录流程。</summary>
    public async Task<LoginOutcome> RunAsync(
        ILoginStrategy strategy,
        LoginContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(strategy);
        ArgumentNullException.ThrowIfNull(context);

        var started = Stopwatch.GetTimestamp();
        context.Config.Statistics.RecordStart(context.Account);

        var maxRetries = context.Config.App.MaxRetries;
        var retries = 0;
        var retryPending = false;
        var outcome = LoginOutcome.Failed;
        string? error = null;

        _logger.LogInformation("开始登录：策略 {Strategy}，账号已脱敏", strategy.Id);

        if (context.Config.Banner.Enabled)
        {
            context.Presenter.ShowBanner();
        }

        while (true)
        {
            try
            {
                if (retryPending)
                {
                    retryPending = false;
                    await _delay(_retryDelay, cancellationToken).ConfigureAwait(false);
                }

                cancellationToken.ThrowIfCancellationRequested();

                context.ReportTask("正在准备登录");
                await PrepareAsync(strategy, context, cancellationToken).ConfigureAwait(false);

                cancellationToken.ThrowIfCancellationRequested();
                context.ReportTask("正在自动登录");
                await strategy.LoginAsync(context, cancellationToken).ConfigureAwait(false);

                context.ReportTask("完成");
                context.ReportProgress("登录完成");
                outcome = LoginOutcome.Success;
                break;
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("登录被取消");
                outcome = LoginOutcome.Cancelled;
                break;
            }
            catch (LoginCancelledException exception)
            {
                // 「已登录跳过」等业务性中止：映射为取消，不计失败、不发失败通知
                _logger.LogInformation("登录被跳过：{Message}", exception.Message);
                outcome = LoginOutcome.Cancelled;
                break;
            }
            catch (Exception exception)
            {
                error = exception.Message;

                var canRetry = exception is not LoginException loginException || loginException.CanRetry;
                if (!canRetry)
                {
                    _logger.LogError("登录失败（不可重试）：{Message}", exception.Message);
                    break;
                }

                if (retries < maxRetries)
                {
                    retries++;

                    // 重试会重新走 prepare，先回收遮罩避免残留在屏幕上
                    context.Presenter.HidePrivacyMask();
                    _logger.LogWarning(
                        "登录失败，{Delay} 秒后重试（{Retries}/{MaxRetries}）：{Message}",
                        _retryDelay.TotalSeconds,
                        retries,
                        maxRetries,
                        exception.Message);

                    retryPending = true;
                    continue;
                }

                _logger.LogCritical("多次尝试均登录失败（{Retries}/{MaxRetries}）：{Message}", retries, maxRetries, exception.Message);
                break;
            }
        }

        await FinalizeAsync(strategy, context, outcome, error).ConfigureAwait(false);

        var elapsed = Stopwatch.GetElapsedTime(started).TotalSeconds;
        context.Config.Statistics.RecordResult(outcome, elapsed);
        _logger.LogInformation("登录流程结束：{Outcome}，耗时 {Elapsed:F2} 秒", outcome, elapsed);

        return outcome;
    }

    private async Task PrepareAsync(ILoginStrategy strategy, LoginContext context, CancellationToken cancellationToken)
    {
        await strategy.BeforePrepareAsync(context, cancellationToken).ConfigureAwait(false);

        context.ReportProgress("获取希沃白板目录");
        var location = _session.ResolvePath();
        if (location.Path is null)
        {
            throw new LoginException("希沃白板目录不存在", canRetry: false);
        }

        context.EasiNotePath = location.Path;

        if (context.Config.Login.SkipIfLoggedIn)
        {
            context.ReportProgress("检查登录状态");
            if (await IsAlreadyLoggedInAsync(context, cancellationToken).ConfigureAwait(false))
            {
                throw new LoginCancelledException("该账号已登录");
            }
        }

        context.ReportProgress("重启希沃进程");
        RestartEasiNote(strategy, context, cancellationToken);

        var title = context.Config.Login.EasiNote.WindowTitle;
        var timeout = TimeSpan.FromSeconds(context.Config.Login.Timeout.LaunchPollingTimeout);
        var interval = TimeSpan.FromSeconds(context.Config.Login.Timeout.LaunchPollingInterval);

        var handle = await _session
            .WaitForWindowAsync(title, context.Config.Debug.AlternateFindWindowMethod, timeout, interval, cancellationToken)
            .ConfigureAwait(false);

        if (handle == 0)
        {
            throw new LoginException($"{title}窗口在{timeout.TotalSeconds}秒内未打开", canRetry: true);
        }

        context.WindowHandle = handle;
        context.ReportTask("等待登录");
        context.ReportProgress("希沃白板已启动");

        await _delay(TimeSpan.FromSeconds(context.Config.Login.Timeout.AfterLaunch), cancellationToken).ConfigureAwait(false);

        if (!_session.BringToFront(handle))
        {
            // 焦点没抢到也继续，只是后续模拟输入可能落到别的窗口（与上游一致）
            _logger.LogWarning("未能将希沃白板窗口切到前台，模拟输入可能落入其他窗口");
        }
    }

    private void RestartEasiNote(ILoginStrategy strategy, LoginContext context, CancellationToken cancellationToken)
    {
        var path = context.EasiNotePath;
        if (path is null || !File.Exists(path))
        {
            // 校验放在终止之前：启动器缺失时不要白白关掉用户的希沃白板
            throw new LoginException("希沃白板可执行文件不存在", canRetry: false);
        }

        _session.KillTargets(
            context.Config.Login.EasiNote.ProcessName,
            context.Config.Login.KillAgent,
            context.Config.Login.EasiNote.ExtraKills,
            context.Config.Login.Timeout.Terminate);

        cancellationToken.ThrowIfCancellationRequested();

        var args = strategy.ResolveLaunchArgs(context);
        context.IsIwb = _session.ResolveIsIwb(args);
        context.LaunchArgs = args;

        try
        {
            _session.Launch(path, args);
        }
        catch (ProcessStartFailedException exception)
        {
            throw new LoginException($"启动希沃白板失败：{exception.Message}", canRetry: true, exception);
        }
    }

    private async Task<bool> IsAlreadyLoggedInAsync(LoginContext context, CancellationToken cancellationToken)
    {
        if (!_session.IsPatched())
        {
            return false;
        }

        var state = await _session.ReadLoginStateAsync(cancellationToken).ConfigureAwait(false);
        if (state is null || !state.IsLoggedIn)
        {
            return false;
        }

        var targetUid = _uidCache.GetLoginUid(context.Account);
        if (string.IsNullOrEmpty(targetUid))
        {
            var auth = await _authenticator.LoginAsync(context.Account, context.Password, cancellationToken).ConfigureAwait(false);
            if (auth.Session is null || string.IsNullOrEmpty(auth.Session.User.Uid))
            {
                return false;
            }

            targetUid = auth.Session.User.Uid;
            _uidCache.SetLoginUid(context.Account, targetUid);
        }

        return string.Equals(state.UserId, targetUid, StringComparison.Ordinal);
    }

    private async Task FinalizeAsync(ILoginStrategy strategy, LoginContext context, LoginOutcome outcome, string? error)
    {
        context.Presenter.HidePrivacyMask();
        context.Presenter.HideBanner();

        try
        {
            await strategy.OnFinishedAsync(context, outcome, error, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "登录收尾回调执行失败");
        }

        context.Presenter.OnFinished(outcome, error);
    }
}

using Microsoft.Extensions.Logging;
using EasiLoginer.Domain.Configuration;

namespace EasiLoginer.Application.Automation;

/// <summary>运行前确认请求（对应上游 <c>Warning</c> 配置）。</summary>
/// <param name="AccountName">账号名（<c>Warning.ShowUserName</c> 为真时显示）。</param>
/// <param name="TimeoutSeconds">等待确认的最大时长。</param>
/// <param name="MaxDelays">最大推迟次数。</param>
/// <param name="DelaySeconds">每次推迟后的等待时长。</param>
public sealed record LoginConfirmationRequest(string? AccountName, int TimeoutSeconds, int MaxDelays, int DelaySeconds);

/// <summary>用户对运行前确认的选择。</summary>
public enum LoginConfirmationChoice
{
    /// <summary>立即执行。</summary>
    Proceed,

    /// <summary>推迟后再问。</summary>
    Delay,

    /// <summary>取消本次登录。</summary>
    Cancel,
}

/// <summary>运行前确认端口（由呈现层实现弹窗；无界面时为 <see langword="null"/>）。</summary>
public interface ILoginConfirmation
{
    /// <summary>请求用户确认。</summary>
    Task<LoginConfirmationChoice> ConfirmAsync(LoginConfirmationRequest request, CancellationToken cancellationToken);
}

/// <summary>前置检查结果。</summary>
public enum PreflightOutcome
{
    /// <summary>继续登录。</summary>
    Proceed,

    /// <summary>命中了「跳过一次」：已消费该标记。</summary>
    SkippedOnce,

    /// <summary>用户取消。</summary>
    CancelledByUser,
}

/// <summary>前置检查结果与"配置是否被改动"。</summary>
/// <param name="Outcome">结果。</param>
/// <param name="ConfigChanged">是否修改了配置（<c>SkipOnce</c> 被清零），调用方需要落盘。</param>
public sealed record PreflightResult(PreflightOutcome Outcome, bool ConfigChanged);

/// <summary>
/// 登录前置检查：<c>SkipOnce</c> → 运行前确认（文档 §2.1 / §6.2）。
/// </summary>
/// <remarks>
/// 档案解锁与凭据解析在组合根完成；这里只处理两件与配置/交互相关的判断。
/// 推迟次数用完后再选推迟视为「继续登录」，与上游一致（推迟是延后，不是取消）。
/// </remarks>
public sealed class LoginPreflight
{
    private readonly ILogger<LoginPreflight> _logger;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;

    /// <summary>创建前置检查。</summary>
    public LoginPreflight(
        ILogger<LoginPreflight> logger,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger;
        _delay = delay ?? ((duration, token) => Task.Delay(duration, token));
    }

    /// <summary>执行前置检查。</summary>
    public async Task<PreflightResult> RunAsync(
        Config config,
        ILoginConfirmation? confirmation,
        string? accountName,
        bool manual,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(config);

        if (config.Login.SkipOnce)
        {
            config.Login.SkipOnce = false;
            _logger.LogInformation("已命中「跳过一次」，本次不登录并清除标记");
            return new PreflightResult(PreflightOutcome.SkippedOnce, ConfigChanged: true);
        }

        var warning = config.Warning;
        if (!warning.Enabled || manual)
        {
            return new PreflightResult(PreflightOutcome.Proceed, ConfigChanged: false);
        }

        if (confirmation is null)
        {
            _logger.LogWarning("当前环境没有确认界面，跳过运行前确认");
            return new PreflightResult(PreflightOutcome.Proceed, ConfigChanged: false);
        }

        var request = new LoginConfirmationRequest(accountName, warning.Timeout, warning.MaxDelays, warning.DelayTime);
        var delays = 0;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var choice = await confirmation.ConfirmAsync(request, cancellationToken).ConfigureAwait(false);

            switch (choice)
            {
                case LoginConfirmationChoice.Proceed:
                    return new PreflightResult(PreflightOutcome.Proceed, ConfigChanged: false);

                case LoginConfirmationChoice.Cancel:
                    _logger.LogInformation("用户取消了本次登录");
                    return new PreflightResult(PreflightOutcome.CancelledByUser, ConfigChanged: false);

                case LoginConfirmationChoice.Delay:
                    if (delays >= warning.MaxDelays)
                    {
                        _logger.LogWarning("推迟次数已用完，继续登录");
                        return new PreflightResult(PreflightOutcome.Proceed, ConfigChanged: false);
                    }

                    delays++;
                    _logger.LogInformation("推迟登录（{Delays}/{MaxDelays}），等待 {Seconds} 秒", delays, warning.MaxDelays, warning.DelayTime);
                    await _delay(TimeSpan.FromSeconds(warning.DelayTime), cancellationToken).ConfigureAwait(false);
                    break;

                default:
                    return new PreflightResult(PreflightOutcome.Proceed, ConfigChanged: false);
            }
        }
    }
}

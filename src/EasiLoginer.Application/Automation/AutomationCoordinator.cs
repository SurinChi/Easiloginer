using Microsoft.Extensions.Logging;
using EasiLoginer.Domain.Automation;
using EasiLoginer.Domain.Configuration;

namespace EasiLoginer.Application.Automation;

/// <summary>
/// 登录任务协调器：单飞 + 按配置选择策略。
/// </summary>
/// <remarks>
/// 上游用 <c>QThread.isRunning()</c> 做单飞；这里用 <see cref="Interlocked"/> 计数，
/// 保证同一时刻最多一个登录任务（文档 §6.7）。
/// </remarks>
public sealed class AutomationCoordinator
{
    private readonly Func<LoginMethod, ILoginStrategy> _strategyFactory;
    private readonly LoginPipeline _pipeline;
    private readonly ILogger<AutomationCoordinator> _logger;
    private int _running;

    /// <summary>创建协调器。</summary>
    public AutomationCoordinator(
        Func<LoginMethod, ILoginStrategy> strategyFactory,
        LoginPipeline pipeline,
        ILogger<AutomationCoordinator> logger)
    {
        ArgumentNullException.ThrowIfNull(strategyFactory);
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(logger);

        _strategyFactory = strategyFactory;
        _pipeline = pipeline;
        _logger = logger;
    }

    /// <summary>是否有登录任务正在运行。</summary>
    public bool IsRunning => Volatile.Read(ref _running) == 1;

    /// <summary>启动一次登录；已有任务在跑时返回 <see langword="null"/>。</summary>
    public async Task<LoginOutcome?> RunAsync(LoginContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (Interlocked.CompareExchange(ref _running, 1, 0) != 0)
        {
            _logger.LogWarning("已有一个正在运行的登录任务，本次请求被忽略");
            return null;
        }

        try
        {
            var strategy = _strategyFactory(context.Config.Login.Method);
            return await _pipeline.RunAsync(strategy, context, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            Interlocked.Exchange(ref _running, 0);
        }
    }
}

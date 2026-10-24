using Microsoft.Extensions.Logging;
using EasiLoginer.Application.Abstractions;

namespace EasiLoginer.Integrations.EasiNote.Process;

/// <summary>希沃白板进程与窗口控制端口（文档 §6.1 / §8.6）。</summary>
public interface IEasiNoteProcessController
{
    /// <summary>终止希沃白板及其附属进程。</summary>
    void KillTargets(string processName, bool killAgent, string extraKills, double timeoutSeconds);

    /// <summary>按参数启动希沃白板。</summary>
    void Launch(string executablePath, string args);

    /// <summary>等待窗口出现；超时返回 0。</summary>
    Task<long> WaitForWindowAsync(string title, bool substring, TimeSpan timeout, TimeSpan pollingInterval, CancellationToken cancellationToken);

    /// <summary>把窗口切到前台；失败返回 <see langword="false"/>。</summary>
    bool BringToFront(long handle, bool pressKey = true);
}

/// <summary>
/// 希沃白板进程控制，对应上游 <c>integrations/easinote/process.py</c> 与
/// <c>BaseAutomator.prepare</c> 中的窗口等待/前台化部分。
/// </summary>
public sealed class EasiNoteProcessController : IEasiNoteProcessController
{
    private readonly IProcessManager _processes;
    private readonly IWindowService _windows;
    private readonly ILogger<EasiNoteProcessController> _logger;

    /// <summary>创建控制器。</summary>
    public EasiNoteProcessController(
        IProcessManager processes,
        IWindowService windows,
        ILogger<EasiNoteProcessController> logger)
    {
        ArgumentNullException.ThrowIfNull(processes);
        ArgumentNullException.ThrowIfNull(windows);
        ArgumentNullException.ThrowIfNull(logger);

        _processes = processes;
        _windows = windows;
        _logger = logger;
    }

    /// <inheritdoc />
    public void KillTargets(string processName, bool killAgent, string extraKills, double timeoutSeconds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(processName);

        var targets = new List<string> { processName };
        if (killAgent)
        {
            targets.Add("EasiAgent");
        }

        if (!string.IsNullOrEmpty(extraKills))
        {
            targets.AddRange(extraKills.Split(','));
        }

        foreach (var raw in targets)
        {
            var name = raw.Trim();

            // 上游用 removesuffix(".exe")，大小写敏感，这里保持一致
            if (name.EndsWith(".exe", StringComparison.Ordinal))
            {
                name = name[..^4];
            }

            if (name.Length == 0)
            {
                continue;
            }

            _processes.Kill(name, force: true, wait: true, timeoutSeconds: timeoutSeconds);
        }
    }

    /// <inheritdoc />
    public void Launch(string executablePath, string args)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);

        var resolved = System.IO.Path.GetFullPath(executablePath);
        var arguments = (args ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

        _logger.LogDebug("启动希沃白板：{Path} 参数 {Args}", resolved, args);
        _processes.Start(resolved, arguments);
    }

    /// <inheritdoc />
    public async Task<long> WaitForWindowAsync(
        string title,
        bool substring,
        TimeSpan timeout,
        TimeSpan pollingInterval,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(title);

        var deadline = DateTimeOffset.UtcNow + timeout;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var handle = _windows.FindWindow(title, substring);
            if (handle != 0)
            {
                return handle;
            }

            if (DateTimeOffset.UtcNow >= deadline)
            {
                _logger.LogWarning("等待窗口 {Title} 超时（{Timeout} 秒）", title, timeout.TotalSeconds);
                return 0;
            }

            await Task.Delay(pollingInterval, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public bool BringToFront(long handle, bool pressKey = true)
    {
        var ok = _windows.SwitchToWindow(handle, pressKey);

        if (!ok)
        {
            // 上游同样只记警告，不中断登录
            _logger.LogWarning("无法把窗口 {Handle:X} 切到前台", handle);
        }

        return ok;
    }
}

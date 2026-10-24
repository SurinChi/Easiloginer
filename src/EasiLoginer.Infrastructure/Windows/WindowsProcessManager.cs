using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using EasiLoginer.Application.Abstractions;

namespace EasiLoginer.Infrastructure.Windows;

/// <summary>
/// 基于 <see cref="Process"/> 的进程控制。
/// </summary>
/// <remarks>
/// 终止与回退策略对应上游 <c>core/process.py::kill_process</c>：
/// 先按进程名枚举并强杀，等待超时只记警告；访问被拒绝时改用
/// <c>taskkill /f /im</c>（参数化调用，不经过 shell，避免引号与编码问题）。
/// </remarks>
public sealed class WindowsProcessManager : IProcessManager
{
    private readonly ILogger<WindowsProcessManager> _logger;

    /// <summary>创建进程控制器。</summary>
    public WindowsProcessManager(ILogger<WindowsProcessManager> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <inheritdoc />
    public void Kill(string processName, bool force = true, bool wait = true, double timeoutSeconds = 1.0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(processName);

        foreach (var process in Process.GetProcessesByName(processName))
        {
            using (process)
            {
                try
                {
                    if (force)
                    {
                        process.Kill();
                    }
                    else
                    {
                        _ = process.CloseMainWindow();
                    }

                    _logger.LogInformation("已向进程 {Name} 发送{Signal}终止信号", processName, force ? "强制" : string.Empty);

                    if (wait && !process.WaitForExit(TimeSpan.FromSeconds(timeoutSeconds)))
                    {
                        _logger.LogWarning("进程 {Name} 关闭超时", processName);
                    }
                }
                catch (InvalidOperationException)
                {
                    _logger.LogWarning("进程 {Name} 已不存在", processName);
                }
                catch (Exception exception) when (exception is Win32Exception or UnauthorizedAccessException or NotSupportedException)
                {
                    _logger.LogWarning("终止进程 {Name} 时访问被拒绝，回退至 taskkill", processName);
                    RunTaskKill(processName, force);
                }
            }
        }
    }

    /// <inheritdoc />
    public void Start(string executablePath, IReadOnlyList<string> arguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        ArgumentNullException.ThrowIfNull(arguments);

        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(executablePath) ?? string.Empty,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        try
        {
            _ = Process.Start(startInfo);
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException or FileNotFoundException)
        {
            throw new ProcessStartFailedException($"启动进程失败：{executablePath}", exception);
        }
    }

    private void RunTaskKill(string processName, bool force)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "taskkill",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        if (force)
        {
            startInfo.ArgumentList.Add("/f");
        }

        startInfo.ArgumentList.Add("/im");
        startInfo.ArgumentList.Add($"{processName}.exe");

        try
        {
            using var process = Process.Start(startInfo);
            process?.WaitForExit(3000);
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
        {
            _logger.LogError(exception, "回退 taskkill 也失败了：{Name}", processName);
        }
    }
}

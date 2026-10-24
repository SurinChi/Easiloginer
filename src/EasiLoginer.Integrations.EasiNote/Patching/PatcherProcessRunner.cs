namespace EasiLoginer.Integrations.EasiNote.Patching;

/// <summary>运行 DllPatcher 的结果。</summary>
/// <param name="ExitCode">退出码；超时时为 -1。</param>
/// <param name="StandardError">标准错误输出（仅用于诊断）。</param>
/// <param name="TimedOut">是否超时。</param>
public sealed record PatcherRunResult(int ExitCode, string StandardError, bool TimedOut);

/// <summary>外部补丁器进程的执行端口（便于测试时替换）。</summary>
public interface IPatcherProcessRunner
{
    /// <summary>运行补丁器并等待退出。</summary>
    Task<PatcherRunResult> RunAsync(
        string executablePath,
        string argument,
        TimeSpan timeout,
        CancellationToken cancellationToken);
}

/// <summary>用 <see cref="System.Diagnostics.Process"/> 运行补丁器：无窗口、30 秒超时。</summary>
public sealed class PatcherProcessRunner : IPatcherProcessRunner
{
    /// <inheritdoc />
    public async Task<PatcherRunResult> RunAsync(
        string executablePath,
        string argument,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);

        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = executablePath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
        };
        startInfo.ArgumentList.Add(argument);

        using var process = System.Diagnostics.Process.Start(startInfo)
            ?? throw new InvalidOperationException($"无法启动补丁器：{executablePath}");

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);

        try
        {
            var standardError = await process.StandardError.ReadToEndAsync(timeoutSource.Token).ConfigureAwait(false);
            await process.WaitForExitAsync(timeoutSource.Token).ConfigureAwait(false);

            return new PatcherRunResult(process.ExitCode, standardError, TimedOut: false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            TryKill(process);
            return new PatcherRunResult(-1, string.Empty, TimedOut: true);
        }
    }

    private static void TryKill(System.Diagnostics.Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // 进程已退出或无权结束，忽略
        }
    }
}

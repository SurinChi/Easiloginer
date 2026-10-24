namespace EasiLoginer.Application.Abstractions;

/// <summary>进程控制端口（终止按进程名，启动按可执行文件 + 参数）。</summary>
/// <remarks>
/// 终止语义与上游 <c>core/process.py::kill_process</c> 对齐：按进程名（不含 <c>.exe</c>）
/// 结束全部同名进程；强制终止后等待若干秒；访问被拒绝时回退到 <c>taskkill</c>。
/// </remarks>
public interface IProcessManager
{
    /// <summary>终止指定名称的全部进程。</summary>
    /// <param name="processName">进程名（不含 <c>.exe</c>）。</param>
    /// <param name="force">强制终止（否则尝试正常关闭主窗口）。</param>
    /// <param name="wait">是否等待进程退出。</param>
    /// <param name="timeoutSeconds">等待超时（秒）。</param>
    void Kill(string processName, bool force = true, bool wait = true, double timeoutSeconds = 1.0);

    /// <summary>启动进程（不经过 shell，参数逐项传入）。</summary>
    /// <exception cref="ProcessStartFailedException">启动失败（文件缺失、权限不足等）。</exception>
    void Start(string executablePath, IReadOnlyList<string> arguments);
}

/// <summary>进程启动失败。</summary>
public sealed class ProcessStartFailedException : Exception
{
    /// <summary>创建异常。</summary>
    public ProcessStartFailedException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

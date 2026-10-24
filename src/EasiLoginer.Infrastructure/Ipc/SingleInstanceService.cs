namespace EasiLoginer.Infrastructure.Ipc;

/// <summary>
/// 单实例判定：全局互斥体 <c>EasiLoginerMutex</c>。
/// </summary>
/// <remarks>
/// 句柄必须持有到进程结束，否则次实例会误判自己为主实例。持有者退出后，
/// 操作系统释放互斥体，下一次启动重新成为主实例（文档 §7.4）。
/// </remarks>
public sealed class SingleInstanceService : IDisposable
{
    private Mutex? _mutex;
    private bool _disposed;

    /// <summary>是否成功成为主实例。</summary>
    public bool IsPrimary { get; private set; }

    /// <summary>尝试获取互斥体。</summary>
    /// <param name="mutexName">互斥体名。</param>
    /// <returns>本进程为主实例返回 <see langword="true"/>。</returns>
    public bool TryAcquire(string mutexName = IpcNames.Mutex)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _mutex = new Mutex(initiallyOwned: false, mutexName, out var createdNew);
        IsPrimary = createdNew;
        return createdNew;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _mutex?.Dispose();
        _disposed = true;
    }
}

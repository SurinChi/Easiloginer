using System.IO.Pipes;
using System.Text.Json;

namespace EasiLoginer.Infrastructure.Ipc;

/// <summary>
/// argv 转发管道客户端：次实例把完整命令行发给主实例后退出。
/// </summary>
public static class ArgvPipeClient
{
    /// <summary>尝试把 argv 帧转发给主实例。</summary>
    /// <param name="pipeName">管道名（不含 <c>\\.\pipe\</c> 前缀）。</param>
    /// <param name="frame">要转发的帧。</param>
    /// <param name="timeout">连接超时；默认 1200 ms（文档 §7.4）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>转发成功返回 <see langword="true"/>；主实例不存在或无响应返回 <see langword="false"/>。</returns>
    public static async Task<bool> TrySendAsync(
        string pipeName,
        ArgvFrame frame,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pipeName);
        ArgumentNullException.ThrowIfNull(frame);

        var effectiveTimeout = timeout ?? TimeSpan.FromMilliseconds(IpcNames.ConnectTimeoutMilliseconds);

        try
        {
            await using var client = new NamedPipeClientStream(
                ".",
                pipeName,
                PipeDirection.Out,
                PipeOptions.Asynchronous);

            await client
                .ConnectAsync((int)effectiveTimeout.TotalMilliseconds, cancellationToken)
                .ConfigureAwait(false);

            await JsonSerializer
                .SerializeAsync(client, frame, IpcJsonContext.Default.ArgvFrame, cancellationToken)
                .ConfigureAwait(false);

            await client.FlushAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}

using System.IO.Pipes;
using System.Text.Json;

namespace EasiLoginer.Infrastructure.Ipc;

/// <summary>
/// argv 转发管道服务端：主实例监听，逐连接读取一个 JSON 帧后回调。
/// </summary>
/// <remarks>
/// 与上游 <c>QLocalServer</c> 行为对齐：同机、当前用户、单连接、读一次即关。
/// 单个连接失败（半包、非法 JSON）不得终止服务循环。
/// </remarks>
public sealed class ArgvPipeServer : IAsyncDisposable
{
    private readonly string _pipeName;
    private readonly Func<ArgvFrame, CancellationToken, Task> _onFrame;
    private readonly CancellationTokenSource _lifetime = new();
    private Task? _loop;

    /// <summary>创建服务端。</summary>
    /// <param name="pipeName">管道名（不含 <c>\\.\pipe\</c> 前缀）。</param>
    /// <param name="onFrame">收到合法帧时的处理回调。</param>
    public ArgvPipeServer(string pipeName, Func<ArgvFrame, CancellationToken, Task> onFrame)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pipeName);
        ArgumentNullException.ThrowIfNull(onFrame);

        _pipeName = pipeName;
        _onFrame = onFrame;
    }

    /// <summary>启动监听循环（幂等）。</summary>
    public void Start() => _loop ??= Task.Run(() => ListenAsync(_lifetime.Token));

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _lifetime.CancelAsync().ConfigureAwait(false);

        if (_loop is not null)
        {
            try
            {
                await _loop.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // 正常停止
            }
        }

        _lifetime.Dispose();
    }

    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using var server = new NamedPipeServerStream(
                    _pipeName,
                    PipeDirection.In,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);

                var frame = await JsonSerializer
                    .DeserializeAsync(server, IpcJsonContext.Default.ArgvFrame, cancellationToken)
                    .ConfigureAwait(false);

                if (frame is not null)
                {
                    await _onFrame(frame, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (IOException)
            {
                // 客户端提前断开：继续等下一条连接
            }
            catch (JsonException)
            {
                // 非法帧：按文档记日志后忽略（日志在 Host 侧接入）
            }
        }
    }
}

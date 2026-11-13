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
    /// <summary>同名管道实例被占满时的重试间隔。</summary>
    private static readonly TimeSpan BindRetryDelay = TimeSpan.FromMilliseconds(500);

    private readonly string _pipeName;
    private readonly Func<ArgvFrame, CancellationToken, Task> _onFrame;
    private readonly Action<Exception>? _onError;
    private readonly CancellationTokenSource _lifetime = new();
    private Task? _loop;

    /// <summary>创建服务端。</summary>
    /// <param name="pipeName">管道名（不含 <c>\\.\pipe\</c> 前缀）。</param>
    /// <param name="onFrame">收到合法帧时的处理回调。</param>
    /// <param name="onError">回调抛异常时的观察者（可选；不传入则忽略）。</param>
    public ArgvPipeServer(
        string pipeName,
        Func<ArgvFrame, CancellationToken, Task> onFrame,
        Action<Exception>? onError = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pipeName);
        ArgumentNullException.ThrowIfNull(onFrame);

        _pipeName = pipeName;
        _onFrame = onFrame;
        _onError = onError;
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
        var bindFailures = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            NamedPipeServerStream server;
            try
            {
                server = new NamedPipeServerStream(
                    _pipeName,
                    PipeDirection.In,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);
                bindFailures = 0;
            }
            catch (IOException exception)
            {
                // 创建失败并不等于「客户端断开」：常见原因是同名管道的实例已被占满
                // （例如另一个登录会话的主实例）。忙等会把一个核心跑满，这里退避后重试。
                bindFailures++;

                // 持续失败时不刷日志：只报第一次，之后每分钟提示一次
                if (bindFailures == 1 || bindFailures % 120 == 0)
                {
                    _onError?.Invoke(exception);
                }

                try
                {
                    await Task.Delay(BindRetryDelay, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                continue;
            }

            await using (server.ConfigureAwait(false))
            {
                try
                {
                    await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);

                    var frame = await JsonSerializer
                        .DeserializeAsync(server, IpcJsonContext.Default.ArgvFrame, cancellationToken)
                        .ConfigureAwait(false);

                    // 帧内容来自进程外：argv 必须是至少含一项的字符串数组，否则丢弃
                    if (frame?.Argv is not { Length: > 0 })
                    {
                        continue;
                    }

                    await DeliverAsync(frame, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch (OperationCanceledException exception)
                {
                    // 处理器自己的取消（例如超时）不代表服务端要停
                    _onError?.Invoke(exception);
                }
                catch (IOException)
                {
                    // 客户端提前断开：继续等下一条连接
                }
                catch (JsonException)
                {
                    // 非法帧：忽略后继续监听
                }
            }
        }
    }

    private async Task DeliverAsync(ArgvFrame frame, CancellationToken cancellationToken)
    {
        try
        {
            await _onFrame(frame, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // 一次转发的命令失败绝不能打死监听循环，否则本进程的单实例转发会永久失效
            _onError?.Invoke(exception);
        }
    }
}

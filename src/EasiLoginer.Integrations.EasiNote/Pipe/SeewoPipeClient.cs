using System.IO.Pipes;
using System.Text;

namespace EasiLoginer.Integrations.EasiNote.Pipe;

/// <summary>
/// 希沃白板命名管道客户端：投递令牌、查询登录态。
/// </summary>
/// <remarks>
/// <para>协议与上游 <c>integrations/easinote/pipe.py</c> 一致：写入<b>一行</b>，读取<b>一行</b>，UTF-8。</para>
/// <para>
/// 与上游的唯一差异：上游用 <c>Path(...).open()</c> 走文件 API，读操作可能无限等待；
/// 这里显式加连接与读取超时（默认 1.2 s + 5 s），避免登录流程被挂死（文档 §8.4 建议）。
/// </para>
/// <para>重试次数与退避由调用方（登录策略）决定，本类只做一次收发。</para>
/// </remarks>
public sealed class SeewoPipeClient
{
    /// <summary>令牌投递管道名（不含 <c>\\.\pipe\</c> 前缀）。</summary>
    public const string DefaultTokenPipeName = "SeewoOpenTokenPipe";

    /// <summary>登录态查询管道名（不含 <c>\\.\pipe\</c> 前缀）。</summary>
    public const string DefaultLoginInfoPipeName = "SeewoLoginInfoPipe";

    private readonly TimeSpan _connectTimeout;
    private readonly TimeSpan _readTimeout;

    /// <summary>创建客户端。</summary>
    /// <param name="connectTimeout">连接超时，默认 1.2 s。</param>
    /// <param name="readTimeout">读取超时，默认 5 s。</param>
    /// <param name="tokenPipeName">令牌管道名；默认 <see cref="DefaultTokenPipeName"/>（测试可替换）。</param>
    /// <param name="loginInfoPipeName">登录态管道名；默认 <see cref="DefaultLoginInfoPipeName"/>。</param>
    public SeewoPipeClient(
        TimeSpan? connectTimeout = null,
        TimeSpan? readTimeout = null,
        string? tokenPipeName = null,
        string? loginInfoPipeName = null)
    {
        _connectTimeout = connectTimeout ?? TimeSpan.FromMilliseconds(1200);
        _readTimeout = readTimeout ?? TimeSpan.FromSeconds(5);
        TokenPipeName = tokenPipeName ?? DefaultTokenPipeName;
        LoginInfoPipeName = loginInfoPipeName ?? DefaultLoginInfoPipeName;
    }

    /// <summary>实际使用的令牌管道名。</summary>
    public string TokenPipeName { get; }

    /// <summary>实际使用的登录态管道名。</summary>
    public string LoginInfoPipeName { get; }

    /// <summary>投递登录令牌并返回白板回执。</summary>
    /// <exception cref="PipeUnavailableException">管道不可用、超时或回执非法。</exception>
    /// <exception cref="PipeDeniedException">权限不足。</exception>
    public async Task<TokenResponse> SendTokenAsync(TokenPayload payload, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var line = await SendAndReadAsync(TokenPipeName, payload.ToJson(), cancellationToken).ConfigureAwait(false);
        return TokenResponse.Parse(line);
    }

    /// <summary>查询白板当前登录态；管道不可用或响应非法时返回 <see langword="null"/>。</summary>
    public async Task<CurrentLoginInfo?> ReadCurrentLoginInfoAsync(
        bool addToLoggedTokens = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var line = await SendAndReadAsync(
                LoginInfoPipeName,
                addToLoggedTokens ? "true" : "false",
                cancellationToken).ConfigureAwait(false);

            return CurrentLoginInfo.TryParse(line);
        }
        catch (PipeException)
        {
            return null;
        }
    }

    /// <summary>向命名管道写入一行并读取一行。</summary>
    internal async Task<string> SendAndReadAsync(string pipeName, string message, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pipeName);
        ArgumentNullException.ThrowIfNull(message);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_connectTimeout + _readTimeout);

        try
        {
            await using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);

            await pipe.ConnectAsync((int)_connectTimeout.TotalMilliseconds, timeout.Token).ConfigureAwait(false);

            var writer = new StreamWriter(pipe, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), leaveOpen: true)
            {
                AutoFlush = false,
                NewLine = "\n",
            };

            await using (writer.ConfigureAwait(false))
            {
                await writer.WriteLineAsync(message.AsMemory(), timeout.Token).ConfigureAwait(false);
                await writer.FlushAsync(timeout.Token).ConfigureAwait(false);
            }

            using var reader = new StreamReader(pipe, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
            var line = await reader.ReadLineAsync(timeout.Token).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(line))
            {
                throw new PipeUnavailableException("管道未返回响应。");
            }

            return line.Trim();
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new PipeUnavailableException("管道响应超时。", exception);
        }
        catch (TimeoutException exception)
        {
            throw new PipeUnavailableException("连接管道超时。", exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new PipeDeniedException("管道访问被拒绝。", exception);
        }
        catch (IOException exception)
        {
            throw new PipeUnavailableException("管道不可用。", exception);
        }
    }
}

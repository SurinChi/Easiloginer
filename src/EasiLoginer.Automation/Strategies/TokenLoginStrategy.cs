using EasiLoginer.Application.Automation;
using EasiLoginer.Domain.Automation;

namespace EasiLoginer.Automation.Strategies;

/// <summary>
/// 令牌投递策略，对应上游 <c>automator/token.py</c>。
/// </summary>
/// <remarks>
/// <para>流程：要求已修补 → API 预登录拿令牌 →（prepare 由管线完成）→ 投递到
/// <c>SeewoOpenTokenPipe</c> → 按回执判定成功/令牌失效/失败。</para>
/// <para>失败语义与上游一致：未修补、凭据错误、风控、管道被拒绝、令牌失效均<b>不可重试</b>；
/// 网络异常与管道未就绪<br>可重试</br>。</para>
/// </remarks>
public sealed class TokenLoginStrategy : ILoginStrategy
{
    /// <summary>管道就绪的最大尝试次数。</summary>
    public const int PipeAttempts = 2;

    /// <summary>两次尝试之间的等待时间。</summary>
    public static readonly TimeSpan PipeRetryDelay = TimeSpan.FromSeconds(1);

    private readonly ISeewoAuthenticator _authenticator;
    private readonly ISeewoTokenPipe _pipe;
    private readonly IEasiNoteSession _session;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;
    private SeewoSession? _login;

    /// <summary>创建策略。</summary>
    public TokenLoginStrategy(
        ISeewoAuthenticator authenticator,
        ISeewoTokenPipe pipe,
        IEasiNoteSession session,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        ArgumentNullException.ThrowIfNull(authenticator);
        ArgumentNullException.ThrowIfNull(pipe);
        ArgumentNullException.ThrowIfNull(session);

        _authenticator = authenticator;
        _pipe = pipe;
        _session = session;
        _delay = delay ?? ((duration, token) => Task.Delay(duration, token));
    }

    /// <inheritdoc />
    public string Id => "token";

    /// <inheritdoc />
    public string ResolveLaunchArgs(LoginContext context) => context.Config.Login.EasiNote.Args;

    /// <inheritdoc />
    public async Task BeforePrepareAsync(LoginContext context, CancellationToken cancellationToken)
    {
        if (!_session.IsPatched())
        {
            throw new LoginException("希沃白板未修补", canRetry: false);
        }

        var result = await _authenticator.LoginAsync(context.Account, context.Password, cancellationToken).ConfigureAwait(false);
        if (result.Session is null)
        {
            throw result.Failure switch
            {
                SeewoAuthFailure.Network => new LoginException("网络异常", canRetry: true),
                SeewoAuthFailure.Credentials => new LoginException("账号或密码错误", canRetry: false),
                SeewoAuthFailure.Captcha => new LoginException("账号被风控", canRetry: false),
                _ => new LoginException("未知登录异常", canRetry: false),
            };
        }

        _login = result.Session;
    }

    /// <inheritdoc />
    public async Task LoginAsync(LoginContext context, CancellationToken cancellationToken)
    {
        var login = _login ?? throw new LoginException("登录会话缺失", canRetry: false);

        var payload = new TokenDeliveryRequest(
            login.Token,
            login.User.Uid,
            login.User.Username,
            login.User.NickName,
            login.User.Phone);

        context.ReportProgress("准备投递令牌");

        for (var attempt = 1; attempt <= PipeAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var response = await _pipe.SendTokenAsync(payload, cancellationToken).ConfigureAwait(false);
                HandleResponse(context, response);
                return;
            }
            catch (TokenPipeDeniedException exception)
            {
                // 权限不足重试无意义
                throw new LoginException($"管道访问被拒绝（权限不足）：{exception.Message}", canRetry: false, exception);
            }
            catch (TokenPipeUnavailableException)
            {
                context.ReportProgress($"等待管道就绪 ({attempt}/{PipeAttempts})");
            }

            if (attempt < PipeAttempts)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await _delay(PipeRetryDelay, cancellationToken).ConfigureAwait(false);
            }
        }

        throw new LoginException("登录管道未启动，请检查希沃白板修补状态", canRetry: false);
    }

    /// <inheritdoc />
    public Task OnFinishedAsync(LoginContext context, LoginOutcome outcome, string? error, CancellationToken cancellationToken)
        => Task.CompletedTask;

    private static void HandleResponse(LoginContext context, TokenDeliveryResponse response)
    {
        if (response.Success)
        {
            context.ReportProgress("登录完成");
            return;
        }

        var message = string.IsNullOrEmpty(response.Message) ? "未知错误" : response.Message;

        if (response.TokenExpired)
        {
            throw new LoginException("令牌已失效，请重新获取", canRetry: false);
        }

        throw new LoginException($"令牌登录失败：{message}", canRetry: true);
    }
}

using EasiLoginer.Domain.Automation;
using EasiLoginer.Domain.Configuration;
using EasiLoginer.Domain.Geometry;

namespace EasiLoginer.Application.Automation;

/// <summary>
/// 登录策略契约，对应上游 <c>BaseAutomator</c> 的四个钩子。
/// </summary>
/// <remarks>
/// 实现位于 <c>EasiLoginer.Automation</c>（固定位置 / 图像识别 / UIA / 令牌投递）。
/// 策略只负责"怎么登录"，准备与重试由 <see cref="LoginPipeline"/> 统一负责。
/// </remarks>
public interface ILoginStrategy
{
    /// <summary>策略标识，用于日志与诊断。</summary>
    string Id { get; }

    /// <summary>本次实际使用的启动参数（固定位置策略会强制 <c>-m Display iwb</c>）。</summary>
    string ResolveLaunchArgs(LoginContext context);

    /// <summary>准备阶段的额外步骤（如令牌策略先做 API 预登录）。</summary>
    Task BeforePrepareAsync(LoginContext context, CancellationToken cancellationToken);

    /// <summary>核心登录动作。</summary>
    Task LoginAsync(LoginContext context, CancellationToken cancellationToken);

    /// <summary>收尾回调（无论成功、失败或取消都会调用一次）。</summary>
    Task OnFinishedAsync(LoginContext context, LoginOutcome outcome, string? error, CancellationToken cancellationToken);
}

/// <summary>屏幕度量端口（实现位于基础设施层）。</summary>
public interface IDisplayMetrics
{
    /// <summary>解析当前主屏度量快照。</summary>
    ScreenMetrics Capture();
}

/// <summary>希沃路径解析结果。</summary>
/// <param name="Path">启动器绝对路径；不存在时为 <see langword="null"/>。</param>
/// <param name="Source">来源标识（registry / fallback / manual）。</param>
public sealed record EasiNotePathResult(string? Path, string Source);

/// <summary>希沃会话控制端口：路径、进程、窗口、补丁状态、管道登录态。</summary>
public interface IEasiNoteSession
{
    /// <summary>解析启动器路径。</summary>
    EasiNotePathResult ResolvePath();

    /// <summary>当前安装是否已修补。</summary>
    bool IsPatched();

    /// <summary>按启动参数判定是否进入白板界面。</summary>
    bool ResolveIsIwb(string args);

    /// <summary>终止目标进程。</summary>
    void KillTargets(string processName, bool killAgent, string extraKills, double timeoutSeconds);

    /// <summary>启动希沃白板。</summary>
    void Launch(string executablePath, string args);

    /// <summary>等待窗口出现；超时返回 0。</summary>
    Task<long> WaitForWindowAsync(string title, bool substring, TimeSpan timeout, TimeSpan interval, CancellationToken cancellationToken);

    /// <summary>把窗口切到前台；失败返回 <see langword="false"/>。</summary>
    bool BringToFront(long handle);

    /// <summary>读取白板当前登录态；不可用时返回 <see langword="null"/>。</summary>
    Task<EasiNoteLoginState?> ReadLoginStateAsync(CancellationToken cancellationToken);
}

/// <summary>档案中的 login_uid 缓存端口。</summary>
public interface ILoginUidCache
{
    /// <summary>按账号读取缓存；无缓存返回 <see langword="null"/>。</summary>
    string? GetLoginUid(string account);

    /// <summary>写入缓存（不落盘由实现决定）。</summary>
    void SetLoginUid(string account, string uid);
}

/// <summary>希沃认证失败分类，用于映射重试语义。</summary>
public enum SeewoAuthFailure
{
    /// <summary>成功。</summary>
    None,

    /// <summary>网络异常（可重试）。</summary>
    Network,

    /// <summary>账号或密码错误（不可重试）。</summary>
    Credentials,

    /// <summary>风控要求验证码（不可重试）。</summary>
    Captcha,

    /// <summary>其它失败（不可重试）。</summary>
    Unknown,
}

/// <summary>希沃认证结果。</summary>
/// <param name="Session">成功时的会话。</param>
/// <param name="Failure">失败分类。</param>
/// <param name="Message">失败消息。</param>
public sealed record SeewoAuthResult(SeewoSession? Session, SeewoAuthFailure Failure, string? Message)
{
    /// <summary>成功结果。</summary>
    public static SeewoAuthResult Success(SeewoSession session) => new(session, SeewoAuthFailure.None, null);

    /// <summary>失败结果。</summary>
    public static SeewoAuthResult Failed(SeewoAuthFailure failure, string message) => new(null, failure, message);
}

/// <summary>希沃账号认证端口（实现为 HTTP 客户端）。</summary>
public interface ISeewoAuthenticator
{
    /// <summary>账号密码登录。</summary>
    Task<SeewoAuthResult> LoginAsync(string account, string password, CancellationToken cancellationToken);
}

/// <summary>令牌投递请求。</summary>
/// <param name="Token">登录令牌。</param>
/// <param name="UserId">用户 ID。</param>
/// <param name="UserName">用户名。</param>
/// <param name="NickName">昵称。</param>
/// <param name="Phone">手机号。</param>
public sealed record TokenDeliveryRequest(string Token, string UserId, string UserName, string NickName, string Phone);

/// <summary>令牌投递回执。</summary>
/// <param name="Success">是否成功。</param>
/// <param name="Message">消息。</param>
/// <param name="ErrorDetail">错误详情。</param>
/// <param name="TokenExpired">令牌是否已失效。</param>
public sealed record TokenDeliveryResponse(bool Success, string Message, string ErrorDetail, bool TokenExpired);

/// <summary>令牌管道不可用（可重试）。</summary>
public sealed class TokenPipeUnavailableException : Exception
{
    /// <summary>创建异常。</summary>
    public TokenPipeUnavailableException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

/// <summary>令牌管道访问被拒绝（不可重试）。</summary>
public sealed class TokenPipeDeniedException : Exception
{
    /// <summary>创建异常。</summary>
    public TokenPipeDeniedException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

/// <summary>令牌投递端口（实现为命名管道客户端）。</summary>
public interface ISeewoTokenPipe
{
    /// <summary>投递令牌并返回回执。</summary>
    Task<TokenDeliveryResponse> SendTokenAsync(TokenDeliveryRequest request, CancellationToken cancellationToken);
}

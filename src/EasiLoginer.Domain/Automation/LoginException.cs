namespace EasiLoginer.Domain.Automation;

/// <summary>
/// 登录流程的业务异常，携带"是否可重试"以驱动管线重试决策（文档 §9.6）。
/// </summary>
public class LoginException : Exception
{
    /// <summary>创建一个可指定重试语义的登录异常。</summary>
    public LoginException(string message, bool canRetry, Exception? innerException = null)
        : base(message, innerException)
        => CanRetry = canRetry;

    /// <summary>是否为可重试错误；不可重试时管线立即失败。</summary>
    public bool CanRetry { get; }
}

/// <summary>用户主动停止或被跳过；管线映射为 <see cref="LoginOutcome.Cancelled"/>，不发失败通知。</summary>
public sealed class LoginCancelledException : Exception
{
    /// <summary>创建一个取消异常。</summary>
    public LoginCancelledException(string message = "登录已被取消。")
        : base(message)
    {
    }
}

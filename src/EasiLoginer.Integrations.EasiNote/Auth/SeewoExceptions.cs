namespace EasiLoginer.Integrations.EasiNote.Auth;

/// <summary>希沃账号服务异常基类。</summary>
public class SeewoLoginException : Exception
{
    /// <summary>创建异常。</summary>
    public SeewoLoginException(string message, int? code = null, string? responseBody = null, Exception? innerException = null)
        : base(message, innerException)
    {
        Code = code;
        ResponseBody = responseBody;
    }

    /// <summary>服务端错误码。</summary>
    public int? Code { get; }

    /// <summary>原始响应体（仅用于诊断，不含密码）。</summary>
    public string? ResponseBody { get; }
}

/// <summary>账号或密码错误。</summary>
public sealed class SeewoAuthException : SeewoLoginException
{
    /// <summary>创建异常。</summary>
    public SeewoAuthException(string message, int code, string? responseBody = null)
        : base(message, code, responseBody)
    {
    }
}

/// <summary>需要图形验证码（风控）。</summary>
public sealed class SeewoCaptchaException : SeewoLoginException
{
    /// <summary>创建异常。</summary>
    public SeewoCaptchaException(string message = "需要验证码", string? responseBody = null)
        : base(message, 4906, responseBody)
    {
    }
}

/// <summary>网络请求失败（可重试）。</summary>
public sealed class SeewoNetworkException : SeewoLoginException
{
    /// <summary>创建异常。</summary>
    public SeewoNetworkException(string message, Exception? innerException = null)
        : base(message, null, null, innerException)
    {
    }
}

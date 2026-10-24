namespace EasiLoginer.Integrations.EasiNote.Pipe;

/// <summary>命名管道相关的基类异常。</summary>
public abstract class PipeException : Exception
{
    /// <summary>创建异常。</summary>
    protected PipeException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

/// <summary>管道不存在、被占用、无响应或超时：属于可重试错误。</summary>
public sealed class PipeUnavailableException : PipeException
{
    /// <summary>创建异常。</summary>
    public PipeUnavailableException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

/// <summary>管道访问被拒绝（权限不足）：重试无意义。</summary>
public sealed class PipeDeniedException : PipeException
{
    /// <summary>创建异常。</summary>
    public PipeDeniedException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

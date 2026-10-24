namespace EasiLoginer.Domain.Profiles;

/// <summary>档案已加密但当前会话未解锁时尝试保存：必须拒绝，避免把空密码写回磁盘。</summary>
public sealed class ProfileLockedException : Exception
{
    /// <summary>创建异常。</summary>
    public ProfileLockedException()
        : base("档案已加密但主密码未解锁，拒绝保存以避免数据丢失。")
    {
    }
}

/// <summary>档案内容不合法（账号或密码为空）。</summary>
public sealed class ProfileValidationException : Exception
{
    /// <summary>创建异常。</summary>
    public ProfileValidationException(string message)
        : base(message)
    {
    }
}

/// <summary>档案文件无法读取或解析。</summary>
public sealed class ProfileLoadException : Exception
{
    /// <summary>创建异常。</summary>
    public ProfileLoadException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

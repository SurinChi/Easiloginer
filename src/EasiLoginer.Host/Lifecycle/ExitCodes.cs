namespace EasiLoginer.Host.Lifecycle;

/// <summary>
/// 进程退出码。前三个是文档 §6.3 的通用约定，20–29 是 patch 子命令的专用码（§8.5）。
/// </summary>
internal static class ExitCodes
{
    /// <summary>成功。</summary>
    public const int Success = 0;

    /// <summary>失败（登录失败、参数非法、转发失败等）。</summary>
    public const int Failure = 1;

    /// <summary>patch 成功。</summary>
    public const int PatchSuccess = 20;

    /// <summary>patch 操作失败。</summary>
    public const int PatchFailed = 21;

    /// <summary>未找到希沃白板。</summary>
    public const int PatchNotFound = 22;

    /// <summary>patch 未知异常。</summary>
    public const int PatchUnknown = 29;
}

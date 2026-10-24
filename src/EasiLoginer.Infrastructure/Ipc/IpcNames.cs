namespace EasiLoginer.Infrastructure.Ipc;

/// <summary>
/// 进程级身份常量。
/// </summary>
/// <remarks>
/// 更名后与上游 EasiAuto <b>不互认</b>：旧版与新版可同时运行，发布说明要求
/// 先退出旧版（文档 §7.4 / §11.3）。名字改动会影响单实例与迁移，勿随意调整。
/// </remarks>
public static class IpcNames
{
    /// <summary>单实例互斥体名。</summary>
    public const string Mutex = "EasiLoginerMutex";

    /// <summary>argv 转发命名管道名。</summary>
    public const string ArgvPipe = "EasiLoginer_Argv_IPC_v1";

    /// <summary>次实例连接主实例的超时（毫秒）。</summary>
    public const int ConnectTimeoutMilliseconds = 1200;
}

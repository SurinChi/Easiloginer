namespace EasiLoginer.Host;

/// <summary>
/// EasiLoginer 进程入口。
/// </summary>
/// <remarks>
/// 命令分发见 <see cref="AppBootstrap"/>：单实例 → argv 转发 → 子命令执行。
/// 登录只由用户、命令行或快捷方式手动触发，不存在定时/外部调度入口。
/// </remarks>
internal static class Program
{
    /// <summary>进程入口，返回进程退出码。</summary>
    /// <param name="args">命令行参数。</param>
    [STAThread]
    private static int Main(string[] args)
        => AppBootstrap.RunAsync(args).GetAwaiter().GetResult();
}

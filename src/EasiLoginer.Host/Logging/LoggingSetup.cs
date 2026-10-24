using System.IO;
using Serilog;
using Serilog.Events;
using EasiLoginer.Infrastructure.Paths;

namespace EasiLoginer.Host.Logging;

/// <summary>
/// 日志装配：控制台 + 按天滚动的文件（文档 §7.5）。
/// </summary>
/// <remarks>
/// 里程碑阶段只装配控制台与文件；Sentry 在配置了 EasiLoginer 自有 DSN 后启用，
/// 未配置时完全关闭，不复用上游 EasiLoginer/EasiAuto 的 DSN。
/// </remarks>
internal static class LoggingSetup
{
    /// <summary>初始化全局日志器。</summary>
    public static void Configure(AppPaths paths)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .Enrich.FromLogContext()
            .WriteTo.Console(restrictedToMinimumLevel: LogEventLevel.Information)
            .WriteTo.Async(sink => sink.File(
                path: Path.Combine(paths.LogsDirectory, "EasiLoginer-.log"),
                rollingInterval: RollingInterval.Day,
                encoding: System.Text.Encoding.UTF8,
                shared: true))
            .CreateLogger();
    }
}

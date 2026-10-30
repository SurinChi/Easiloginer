using System.IO;
using Serilog;
using Serilog.Events;
using EasiLoginer.Infrastructure.Paths;

namespace EasiLoginer.Host.Logging;

/// <summary>
/// 日志装配：控制台 + 按天滚动的文件（文档 §7.5）。
/// </summary>
/// <remarks>
/// 不上报任何遥测：崩溃与诊断信息只写本地文件，由用户自行决定是否提供。
/// </remarks>
internal static class LoggingSetup
{
    /// <summary>初始化全局日志器。</summary>
    public static void Configure(AppPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

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

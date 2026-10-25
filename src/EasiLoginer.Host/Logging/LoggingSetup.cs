using System.IO;
using Serilog;
using Serilog.Events;
using EasiLoginer.Infrastructure.Paths;

namespace EasiLoginer.Host.Logging;

/// <summary>
/// 日志装配：控制台 + 按天滚动的文件 + 可选的 Sentry（文档 §7.5）。
/// </summary>
/// <remarks>
/// Sentry 只在 <see cref="TelemetrySettings"/> 判定为启用、且 SDK 成功初始化时接入；
/// 未配置时完全关闭，不复用上游 EasiLoginer/EasiAuto 的 DSN。
/// </remarks>
internal static class LoggingSetup
{
    /// <summary>初始化全局日志器。</summary>
    public static void Configure(AppPaths paths, TelemetrySettings telemetry)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(telemetry);

        var configuration = new LoggerConfiguration()
            .MinimumLevel.Information()
            .Enrich.FromLogContext()
            .WriteTo.Console(restrictedToMinimumLevel: LogEventLevel.Information)
            .WriteTo.Async(sink => sink.File(
                path: Path.Combine(paths.LogsDirectory, "EasiLoginer-.log"),
                rollingInterval: RollingInterval.Day,
                encoding: System.Text.Encoding.UTF8,
                shared: true));

        if (telemetry is { Enabled: true, Dsn: { Length: > 0 } dsn })
        {
            configuration = ConfigureSentry(configuration, dsn, telemetry.Version);
        }

        Log.Logger = configuration.CreateLogger();
    }

    private static LoggerConfiguration ConfigureSentry(LoggerConfiguration configuration, string dsn, string version)
    {
        // 初始化失败（例如被 Smart App Control 拦截）时退回纯本地日志
        try
        {
            return SentryIntegration.Attach(configuration, dsn, version);
        }
        catch (Exception exception) when (exception is FileLoadException or FileNotFoundException
                                              or TypeLoadException or BadImageFormatException or DllNotFoundException)
        {
            // 程序集在 JIT 阶段就加载失败时，异常会抛到调用点，这里兜住
            Log.Warning(exception, "Sentry SDK 无法加载，已跳过遥测（不影响正常使用）");
            return configuration;
        }
    }
}

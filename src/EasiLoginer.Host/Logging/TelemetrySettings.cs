using System.IO;
using System.Reflection;
using EasiLoginer.Infrastructure.Configuration;
using EasiLoginer.Infrastructure.Paths;
using EasiLoginer.Host.Logging;
using Serilog;

namespace EasiLoginer.Host.Logging;

/// <summary>
/// 遥测开关注解：只有「用户在配置里启用」且「提供了 DSN」时才上报（文档 §5.1）。
/// </summary>
/// <param name="Enabled">是否启用遥测。</param>
/// <param name="Dsn">Sentry DSN；未配置时为空。</param>
/// <param name="Version">用于 Sentry Release 的版本号。</param>
internal sealed record TelemetrySettings(bool Enabled, string? Dsn, string Version)
{
    /// <summary>DSN 的环境变量名。</summary>
    public const string DsnVariable = "EASILOGINER_SENTRY_DSN";

    /// <summary>DSN 也可以放在数据目录的这个文件里（<c>data/</c> 已被 gitignore）。</summary>
    public const string DsnFileName = "sentry.dsn";

    /// <summary>读取配置与 DSN；任何异常都退化为「关闭」。</summary>
    public static TelemetrySettings Load(AppPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        var enabled = false;
        try
        {
            enabled = new ConfigStore(paths, new SerilogLogger<ConfigStore>()).Load().App.TelemetryEnabled;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            // 配置损坏时按关闭处理，绝不因为遥测影响启动
        }

        var dsn = Environment.GetEnvironmentVariable(DsnVariable);
        if (string.IsNullOrWhiteSpace(dsn))
        {
            dsn = ReadDsnFile(paths);
        }

        return new TelemetrySettings(enabled, string.IsNullOrWhiteSpace(dsn) ? null : dsn.Trim(), ResolveVersion());
    }

    private static string? ReadDsnFile(AppPaths paths)
    {
        try
        {
            var path = Path.Combine(paths.DataDirectory, DsnFileName);
            return File.Exists(path) ? File.ReadAllText(path).Trim() : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string ResolveVersion()
    {
        var assembly = typeof(TelemetrySettings).Assembly;
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        return informational?.Split('+')[0] ?? assembly.GetName().Version?.ToString() ?? "0.0.0";
    }
}

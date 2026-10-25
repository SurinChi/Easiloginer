using System.IO;
using EasiLoginer.Application.Observability;
using Serilog;
using Serilog.Configuration;
using Sentry;

namespace EasiLoginer.Host.Logging;

/// <summary>
/// Sentry 接入：由 Serilog sink 完成初始化，全局 Hub 供手动上报使用。
/// </summary>
/// <remarks>
/// <para>只在<b>用户启用遥测且配置了 DSN</b> 时接入；默认完全关闭。</para>
/// <para>本机开启了 Smart App Control，未签名的 <c>Sentry.dll</c> 可能被策略拦截（0x800711C7）。
/// 接入失败只记一条警告，不影响程序启动；<see cref="IsAvailable"/> 保持 false，
/// 之后所有上报调用都会跳过（避免再次触发程序集加载）。</para>
/// </remarks>
internal static class SentryIntegration
{
    /// <summary>SDK 是否已成功接入。</summary>
    public static bool IsAvailable { get; private set; }

    /// <summary>把 Sentry sink 接到日志管道上；成功即视为可用。</summary>
    public static LoggerConfiguration Attach(LoggerConfiguration configuration, string dsn, string version)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(dsn);

        var attached = configuration.WriteTo.Sentry(options =>
        {
            // Sink 内部会用它初始化 Sentry；缺 DSN 会直接抛异常
            options.Dsn = dsn;

            // 与 GitHub Release 的 tag 保持一致（v0.0.1），这样 Sentry 里能直接对上发布
            options.Release = $"v{version}";
            options.Environment = "production";
            options.SendDefaultPii = false;   // 不收集 IP / 用户名等默认隐私信息
            options.TracesSampleRate = 0.0;   // 只收错误，不做性能采样
            options.AttachStacktrace = true;
            options.SetBeforeSend(Scrub);
        });

        IsAvailable = true;
        return attached;
    }

    /// <summary>上报异常；未接入时静默跳过。</summary>
    public static void Capture(Exception exception)
    {
        if (!IsAvailable)
        {
            return;
        }

        SentrySdk.CaptureException(exception);
    }

    /// <summary>退出前尽量把事件送出去。</summary>
    public static async Task FlushAsync()
    {
        if (!IsAvailable)
        {
            return;
        }

        await SentrySdk.FlushAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
    }

    private static SentryEvent? Scrub(SentryEvent @event, SentryHint hint)
    {
        // SendDefaultPii=false 已避免 IP/用户等默认隐私字段；这里只处理自由文本
        if (@event.Message?.Message is { Length: > 0 } message)
        {
            @event.Message = SensitiveDataScrubber.Scrub(message);
        }

        if (@event.SentryExceptions is { } exceptions)
        {
            foreach (var exception in exceptions)
            {
                if (exception.Value is { Length: > 0 } value)
                {
                    exception.Value = SensitiveDataScrubber.Scrub(value);
                }
            }
        }

        return @event;
    }
}

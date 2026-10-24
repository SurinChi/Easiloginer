using Microsoft.Extensions.Logging;
using Serilog.Events;

namespace EasiLoginer.Host.Logging;

/// <summary>
/// 把 Serilog 适配成 <see cref="ILogger{T}"/>，让基础设施/应用层的结构化日志
/// 汇入同一份日志文件（文档 §7.5）。
/// </summary>
internal sealed class SerilogLogger<T> : ILogger<T>
{
    private static readonly Serilog.ILogger Logger = Serilog.Log.ForContext<T>();

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
        => null;

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    /// <inheritdoc />
    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);

        var level = logLevel switch
        {
            LogLevel.Trace => LogEventLevel.Verbose,
            LogLevel.Debug => LogEventLevel.Debug,
            LogLevel.Information => LogEventLevel.Information,
            LogLevel.Warning => LogEventLevel.Warning,
            LogLevel.Error => LogEventLevel.Error,
            LogLevel.Critical => LogEventLevel.Fatal,
            _ => LogEventLevel.Information,
        };

        Logger.Write(level, exception, formatter(state, exception));
    }
}

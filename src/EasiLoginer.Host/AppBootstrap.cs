using Serilog;
using EasiLoginer.Host.CommandLine;
using EasiLoginer.Host.Lifecycle;
using EasiLoginer.Host.Logging;
using EasiLoginer.Infrastructure.Ipc;
using EasiLoginer.Infrastructure.Paths;

namespace EasiLoginer.Host;

/// <summary>
/// 进程启动编排：路径 → 日志 → 全局异常 → 单实例 → 命令分发（文档 §6.3 / §7.5）。
/// </summary>
internal static class AppBootstrap
{
    /// <summary>可被次实例转发的命令；<c>patch</c> 永远不转发。</summary>
    private static readonly string[] ForwardableCommands = ["settings", "login", "oobe", "skip"];

    /// <summary>进程入口逻辑，返回退出码。</summary>
    public static async Task<int> RunAsync(string[] args)
    {
        var paths = AppPaths.Default;
        paths.EnsureWritableDirectories();

        // 遥测开关要在装配日志之前确定：Sentry sink 只能在 LoggerConfiguration 阶段加入
        var telemetry = TelemetrySettings.Load(paths);
        LoggingSetup.Configure(paths, telemetry);

        if (SentryIntegration.IsAvailable)
        {
            Log.Information("Sentry 已启用，Release v{Version}，仅上报错误级事件", telemetry.Version);
        }

        RegisterGlobalHandlers();

        try
        {
            return await RunCoreAsync(args, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            Log.Fatal(exception, "未处理异常，进程退出");
            if (SentryIntegration.IsAvailable)
            {
                SentryIntegration.Capture(exception);
            }

            return ExitCodes.Failure;
        }
        finally
        {
            if (SentryIntegration.IsAvailable)
            {
                await SentryIntegration.FlushAsync().ConfigureAwait(false);
            }

            await Log.CloseAndFlushAsync().ConfigureAwait(false);
        }
    }

    /// <summary>全局异常兜底：任何未处理异常都必须留下日志（文档 §7.5）。</summary>
    private static void RegisterGlobalHandlers()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
        {
            Log.Fatal(eventArgs.ExceptionObject as Exception, "AppDomain 未处理异常，进程即将终止");

            if (SentryIntegration.IsAvailable && eventArgs.ExceptionObject is Exception exception)
            {
                SentryIntegration.Capture(exception);
            }

            Log.CloseAndFlush();
        };

        TaskScheduler.UnobservedTaskException += (_, eventArgs) =>
        {
            Log.Error(eventArgs.Exception, "未观察的任务异常");

            if (SentryIntegration.IsAvailable)
            {
                SentryIntegration.Capture(eventArgs.Exception);
            }

            eventArgs.SetObserved();
        };
    }

    private static async Task<int> RunCoreAsync(string[] args, CancellationToken cancellationToken)
    {
        var root = CommandFactory.Create();
        var parseResult = root.Parse(args);

        // patch 绕过单实例（文档 §6.3）：它可以提权运行，不能与主实例抢管道
        if (IsPatch(parseResult))
        {
            return await parseResult.InvokeAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        using var instance = new SingleInstanceService();
        if (!instance.TryAcquire())
        {
            return await ForwardAsync(parseResult, args, cancellationToken).ConfigureAwait(false);
        }

        await using var server = new ArgvPipeServer(
            IpcNames.ArgvPipe,
            (frame, token) => HandleForwardedAsync(root, frame, token));

        server.Start();
        return await parseResult.InvokeAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private static bool IsPatch(System.CommandLine.ParseResult parseResult)
        => string.Equals(parseResult.CommandResult.Command.Name, "patch", StringComparison.OrdinalIgnoreCase);

    private static async Task<int> ForwardAsync(
        System.CommandLine.ParseResult parseResult,
        string[] args,
        CancellationToken cancellationToken)
    {
        var commandName = parseResult.CommandResult.Command is System.CommandLine.RootCommand
            ? string.Empty
            : parseResult.CommandResult.Command.Name;

        var forwardable = commandName.Length == 0 ||
                          ForwardableCommands.Contains(commandName, StringComparer.OrdinalIgnoreCase);

        if (!forwardable)
        {
            Log.Information("命令 {Command} 不可转发，次实例直接退出", commandName);
            return ExitCodes.Success;
        }

        var frame = new ArgvFrame([Environment.ProcessPath ?? "EasiLoginer.exe", .. args]);
        var forwarded = await ArgvPipeClient
            .TrySendAsync(IpcNames.ArgvPipe, frame, null, cancellationToken)
            .ConfigureAwait(false);

        Log.Information("argv 转发{Result}", forwarded ? "成功" : "失败");
        return forwarded ? ExitCodes.Success : ExitCodes.Failure;
    }

    private static async Task HandleForwardedAsync(
        System.CommandLine.RootCommand root,
        ArgvFrame frame,
        CancellationToken cancellationToken)
    {
        var argv = frame.Argv.Length > 1 ? frame.Argv[1..] : [];
        Log.Information("收到转发命令：{Argv}", string.Join(' ', argv));

        var exitCode = await root
            .Parse(argv)
            .InvokeAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        Log.Information("转发命令执行完成，退出码 {ExitCode}", exitCode);
    }
}

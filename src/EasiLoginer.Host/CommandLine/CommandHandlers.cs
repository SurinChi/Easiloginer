using System.IO;
using System.Net.Http;
using System.Threading;
using Serilog;
using EasiLoginer.Host.Automation;
using EasiLoginer.Host.Lifecycle;
using EasiLoginer.Host.Logging;
using EasiLoginer.Host.Services;
using EasiLoginer.Infrastructure.Configuration;
using EasiLoginer.Infrastructure.Paths;
using EasiLoginer.Infrastructure.Profiles;
using EasiLoginer.Infrastructure.Update;
using EasiLoginer.Infrastructure.Windows;
using EasiLoginer.Presentation;

namespace EasiLoginer.Host.CommandLine;

/// <summary>
/// 命令处理器。UI 命令在专用 STA 线程上运行 WPF 消息循环，避免被后台线程的
/// 异步续体影响（WPF 要求 STA）。
/// </summary>
internal static class CommandHandlers
{
    /// <summary>更新检查共用的 HTTP 客户端（GitHub API 要求 User-Agent，超时保守）。</summary>
    private static readonly HttpClient UpdateHttpClient = new() { Timeout = TimeSpan.FromSeconds(20) };

    /// <summary>打开主界面。</summary>
    public static int Settings()
    {
        try
        {
            var paths = AppPaths.Default;
            var configStore = new ConfigStore(paths, new SerilogLogger<ConfigStore>());
            var config = configStore.Load();

            var profiles = new JsonProfileRepository(paths, new SerilogLogger<JsonProfileRepository>());
            profiles.Load();

            var services = new AppServices(
                new ConfigServiceAdapter(configStore, config),
                profiles,
                new LoginRunnerAdapter(),
                new WindowsDisplayMetrics(),
                new GitHubUpdateSource(UpdateHttpClient));

            var exitCode = 0;
            var thread = new Thread(() =>
            {
                try
                {
                    var app = new App(services);
                    exitCode = app.Run();
                }
                catch (Exception exception)
                {
                    Log.Fatal(exception, "主界面异常退出");
                    exitCode = ExitCodes.Failure;
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            return exitCode;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Log.Error(exception, "打开主界面失败");
            return ExitCodes.Failure;
        }
    }

    /// <summary>执行一次手动登录。</summary>
    public static async Task<int> LoginAsync(
        string? profileId,
        string? account,
        string? password,
        bool manual,
        CancellationToken cancellationToken)
    {
        if (profileId is not null && account is not null)
        {
            Log.Warning("login 参数冲突：--id 与 --account 互斥");
            return ExitCodes.Failure;
        }

        if (profileId is null && account is null)
        {
            Log.Warning("login 缺少凭据：需要 --id，或 --account 与 --password");
            return ExitCodes.Failure;
        }

        if (account is not null && password is null)
        {
            Log.Warning("login 使用 --account 时必须同时提供 --password");
            return ExitCodes.Failure;
        }

        Log.Information(
            "命令 login：档案={ProfileId} 账号={MaskedAccount} 手动={Manual}",
            profileId ?? "(无)",
            Mask(account),
            manual);

        return await LoginRunner
            .RunAsync(profileId, account, password, manual, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>重新进入首次向导：重置标记后打开主界面，由界面弹出向导。</summary>
    public static int Oobe()
    {
        try
        {
            var store = new ConfigStore(AppPaths.Default, new SerilogLogger<ConfigStore>());
            var config = store.Load();
            config.Internal.IsOobeCompleted = false;
            store.Save(config);

            Log.Information("已重置首次向导标记，正在打开向导");
            return Settings();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Log.Error(exception, "重置首次向导标记失败");
            return ExitCodes.Failure;
        }
    }

    /// <summary>写一次 SkipOnce 后退出（对应 <c>skip</c> 子命令）。</summary>
    public static int Skip()
    {
        try
        {
            var store = new ConfigStore(AppPaths.Default, new SerilogLogger<ConfigStore>());
            var config = store.Load();
            config.Login.SkipOnce = true;
            store.Save(config);

            Log.Information("已设置 SkipOnce=true，下次触发登录时将跳过");
            return ExitCodes.Success;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Log.Error(exception, "写入 SkipOnce 失败");
            return ExitCodes.Failure;
        }
    }

    /// <summary>修补/撤销希沃白板（M3 逻辑已就绪，等待 UI 与提权路径接入）。</summary>
    public static int Patch(bool on, bool off)
    {
        if (on == off)
        {
            Log.Warning("patch 必须且只能指定 --on 或 --off 之一");
            return ExitCodes.PatchUnknown;
        }

        Log.Information("命令 patch：命令行修补尚未接入 UI 提权流程，目标={Target}", on ? "on" : "off");
        return ExitCodes.PatchUnknown;
    }

    /// <summary>日志脱敏：账号只保留首尾字符。</summary>
    private static string Mask(string? account)
    {
        if (string.IsNullOrEmpty(account))
        {
            return "(无)";
        }

        return account.Length <= 2
            ? new string('*', account.Length)
            : $"{account[0]}{new string('*', account.Length - 2)}{account[^1]}";
    }
}

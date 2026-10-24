using System.IO;
using System.Net.Http;
using Serilog;
using EasiLoginer.Application.Abstractions;
using EasiLoginer.Application.Automation;
using EasiLoginer.Application.Profiles;
using EasiLoginer.Automation.Input;
using EasiLoginer.Automation.Strategies;
using EasiLoginer.Automation.Uia;
using EasiLoginer.Domain.Automation;
using EasiLoginer.Domain.Configuration;
using EasiLoginer.Domain.Profiles;
using EasiLoginer.Host.Lifecycle;
using EasiLoginer.Host.Logging;
using EasiLoginer.Infrastructure.Configuration;
using EasiLoginer.Infrastructure.Paths;
using EasiLoginer.Infrastructure.Profiles;
using EasiLoginer.Infrastructure.Windows;
using EasiLoginer.Integrations.EasiNote.Auth;
using EasiLoginer.Integrations.EasiNote.Environment;
using EasiLoginer.Integrations.EasiNote.Patching;
using EasiLoginer.Integrations.EasiNote.Path;
using EasiLoginer.Integrations.EasiNote.Pipe;
using EasiLoginer.Integrations.EasiNote.Process;

namespace EasiLoginer.Host.Automation;

/// <summary>
/// 登录的组合根：读配置与档案 → 组装端口 → 跑登录管线。
/// </summary>
/// <remarks>
/// 只有这里同时依赖 Application 端口与 Integrations 实现（组合根例外，文档 §4.3 规则 5）。
/// 登录仍只由用户手动触发；本类不包含任何定时或外部调度逻辑。
/// </remarks>
public static class LoginRunner
{
    /// <summary>执行一次手动登录，返回进程退出码（CLI 用）。</summary>
    public static async Task<int> RunAsync(
        string? profileId,
        string? account,
        string? password,
        bool manual,
        CancellationToken cancellationToken,
        ILoginPresenter? presenter = null)
    {
        var (_, exitCode) = await ExecuteAsync(profileId, account, password, manual, cancellationToken, presenter)
            .ConfigureAwait(false);

        return exitCode;
    }

    /// <summary>按档案执行登录并返回结果（UI 用）。</summary>
    public static async Task<LoginOutcome> RunProfileAsync(
        string profileId,
        LoginRunOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);

        var (outcome, _) = await ExecuteAsync(
                profileId,
                null,
                null,
                manual: false,
                cancellationToken,
                options.Presenter,
                options.Confirmation)
            .ConfigureAwait(false);

        return outcome ?? LoginOutcome.Failed;
    }

    private static async Task<(LoginOutcome? Outcome, int ExitCode)> ExecuteAsync(
        string? profileId,
        string? account,
        string? password,
        bool manual,
        CancellationToken cancellationToken,
        ILoginPresenter? presenter = null,
        ILoginConfirmation? confirmation = null)
    {
        var paths = AppPaths.Default;

        try
        {
            var configStore = new ConfigStore(paths, new SerilogLogger<ConfigStore>());
            var config = configStore.Load();

            var profiles = new JsonProfileRepository(paths, new SerilogLogger<JsonProfileRepository>());
            var profile = profiles.Load();

            var credentials = ResolveCredentials(profiles, profile, profileId, account, password);
            if (credentials is null)
            {
                return (null, ExitCodes.Failure);
            }

            // 前置：跳过一次 / 运行前确认（文档 §2.1 的五道前置）
            var preflight = new LoginPreflight(new SerilogLogger<LoginPreflight>());
            var preflightResult = await preflight
                .RunAsync(config, confirmation, credentials.Value.Account, manual, cancellationToken)
                .ConfigureAwait(false);

            if (preflightResult.ConfigChanged)
            {
                configStore.Save(config);
            }

            if (preflightResult.Outcome is PreflightOutcome.SkippedOnce or PreflightOutcome.CancelledByUser)
            {
                Log.Information("前置检查未通过：{Outcome}", preflightResult.Outcome);
                return (LoginOutcome.Cancelled, ExitCodes.Success);
            }

            try
            {
                LoginMethodSupport.EnsureImplemented(config.Login.Method);
            }
            catch (NotSupportedException exception)
            {
                Log.Error(exception, "登录方式不可用");
                return (null, ExitCodes.Failure);
            }

            var outcome = await RunPipelineAsync(
                    paths,
                    config,
                    profiles,
                    credentials.Value,
                    manual,
                    presenter ?? NullLoginPresenter.Instance,
                    cancellationToken)
                .ConfigureAwait(false);

            // 统计计数需要落盘
            configStore.Save(config);

            Log.Information("登录结果：{Outcome}", outcome);

            var exitCode = outcome switch
            {
                // 用户主动停止或"已登录跳过"都不算失败
                LoginOutcome.Success => ExitCodes.Success,
                LoginOutcome.Cancelled => ExitCodes.Success,
                _ => ExitCodes.Failure,
            };

            return (outcome, exitCode);
        }
        catch (ProfileLoadException exception)
        {
            Log.Error(exception, "档案读取失败");
            return (null, ExitCodes.Failure);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            Log.Error(exception, "登录失败");
            return (null, ExitCodes.Failure);
        }
    }

    private static (string Account, string Password)? ResolveCredentials(
        JsonProfileRepository profiles,
        Profile profile,
        string? profileId,
        string? account,
        string? password)
    {
        if (profileId is null)
        {
            return account is not null && password is not null ? (account, password) : null;
        }

        if (profile.EncryptionEnabled && !profiles.IsUnlocked && !profiles.TryCachedUnlock())
        {
            Log.Error("档案已加密且本机无法自动解锁，请在应用中输入主密码后再试");
            return null;
        }

        var automation = profiles.FindAutomation(profileId);
        if (automation is null)
        {
            Log.Error("未找到档案 {ProfileId}", profileId);
            return null;
        }

        if (string.IsNullOrWhiteSpace(automation.Account) || string.IsNullOrWhiteSpace(automation.Password))
        {
            Log.Error("档案 {ProfileId} 的账号或密码为空", profileId);
            return null;
        }

        return (automation.Account, automation.Password);
    }

    private static async Task<LoginOutcome> RunPipelineAsync(
        AppPaths paths,
        Config config,
        IProfileRepository profiles,
        (string Account, string Password) credentials,
        bool manual,
        ILoginPresenter presenter,
        CancellationToken cancellationToken)
    {
        var display = new WindowsDisplayMetrics();
        var screen = display.Capture();

        var locator = new EasiNoteLocator(new WindowsRegistryReader());
        var windows = new WindowsWindowService(new SerilogLogger<WindowsWindowService>());
        var processes = new WindowsProcessManager(new SerilogLogger<WindowsProcessManager>());
        var controller = new EasiNoteProcessController(
            processes, windows, new SerilogLogger<EasiNoteProcessController>());

        var patcher = new EasiNotePatcher(
            System.IO.Path.Combine(paths.BaseDirectory, "vendors"),
            new PatcherProcessRunner(),
            new SerilogLogger<EasiNotePatcher>());

        var pipe = new SeewoPipeClient();
        var detector = new IwbDetector(FkvConfigSource.FromEnvironment(), new WindowsHidDeviceEnumerator());

        var session = new EasiNoteSessionAdapter(
            locator, controller, patcher, pipe, detector, () => config.Login.EasiNote);

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        var authenticator = new SeewoAuthenticatorAdapter(new SeewoAuthClient(http));
        var tokenPipe = new SeewoTokenPipeAdapter(pipe);

        var input = new SendInputSimulator(
            new WindowsInputBackend(),
            screen,
            config.Login.ForceCompatibilityMode,
            new SerilogLogger<SendInputSimulator>());

        var uidCache = new ProfileLoginUidCache(profiles);
        var pipeline = new LoginPipeline(session, uidCache, authenticator, new SerilogLogger<LoginPipeline>());

        ILoginStrategy StrategyFactory(LoginMethod method) => method switch
        {
            LoginMethod.Token => new TokenLoginStrategy(authenticator, tokenPipe, session),
            LoginMethod.Uia => new UiaLoginStrategy(new WindowsUiAutomation()),
            _ => new FixedPositionStrategy(input, session, display),
        };

        var coordinator = new AutomationCoordinator(
            StrategyFactory, pipeline, new SerilogLogger<AutomationCoordinator>());

        var context = new LoginContext(credentials.Account, credentials.Password, config, presenter)
        {
            Screen = screen,
        };

        if (manual)
        {
            Log.Information("已跳过运行前确认（--manual）");
        }

        var outcome = await coordinator.RunAsync(context, cancellationToken).ConfigureAwait(false);
        return outcome ?? LoginOutcome.Failed;
    }
}


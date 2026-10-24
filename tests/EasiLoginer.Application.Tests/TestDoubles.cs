using EasiLoginer.Application.Automation;
using EasiLoginer.Automation.Input;
using EasiLoginer.Domain.Automation;
using EasiLoginer.Domain.Configuration;
using EasiLoginer.Domain.Geometry;

namespace EasiLoginer.Application.Tests;

/// <summary>登录管线与策略测试共用的假实现。</summary>
internal sealed class FakeSession : IEasiNoteSession
{
    public string? Path { get; set; }

    public string PathSource { get; set; } = "registry";

    public bool Patched { get; set; }

    public bool IsIwb { get; set; }

    public EasiNoteLoginState? LoginState { get; set; }

    public long WindowHandle { get; set; } = 0x1234;

    public bool BringToFrontResult { get; set; } = true;

    public List<(string Process, bool KillAgent, string Extra, double Timeout)> Kills { get; } = [];

    public List<(string Path, string Args)> Launches { get; } = [];

    public int WaitCalls { get; private set; }

    public EasiNotePathResult ResolvePath() => new(Path, PathSource);

    public bool IsPatched() => Patched;

    public bool ResolveIsIwb(string args) => IsIwb;

    public void KillTargets(string processName, bool killAgent, string extraKills, double timeoutSeconds)
        => Kills.Add((processName, killAgent, extraKills, timeoutSeconds));

    public void Launch(string executablePath, string args) => Launches.Add((executablePath, args));

    public Task<long> WaitForWindowAsync(string title, bool substring, TimeSpan timeout, TimeSpan interval, CancellationToken cancellationToken)
    {
        WaitCalls++;
        return Task.FromResult(WindowHandle);
    }

    public bool BringToFront(long handle) => BringToFrontResult;

    public Task<EasiNoteLoginState?> ReadLoginStateAsync(CancellationToken cancellationToken)
        => Task.FromResult(LoginState);
}

/// <summary>可编排失败次数的策略桩。</summary>
internal sealed class ScriptedStrategy : ILoginStrategy
{
    public Queue<Exception?> Failures { get; } = new();

    public int LoginCalls { get; private set; }

    public int BeforePrepareCalls { get; private set; }

    public int FinishedCalls { get; private set; }

    public LoginOutcome? LastOutcome { get; private set; }

    public string? LastError { get; private set; }

    public string LaunchArgs { get; set; } = string.Empty;

    public string Id => "scripted";

    public string ResolveLaunchArgs(LoginContext context) => LaunchArgs;

    public Task BeforePrepareAsync(LoginContext context, CancellationToken cancellationToken)
    {
        BeforePrepareCalls++;
        return Task.CompletedTask;
    }

    public Task LoginAsync(LoginContext context, CancellationToken cancellationToken)
    {
        LoginCalls++;
        if (Failures.Count > 0)
        {
            var failure = Failures.Dequeue();
            if (failure is not null)
            {
                throw failure;
            }
        }

        return Task.CompletedTask;
    }

    public Task OnFinishedAsync(LoginContext context, LoginOutcome outcome, string? error, CancellationToken cancellationToken)
    {
        FinishedCalls++;
        LastOutcome = outcome;
        LastError = error;
        return Task.CompletedTask;
    }
}

internal sealed class RecordingPresenter : ILoginPresenter
{
    public List<string> Tasks { get; } = [];

    public List<string> Progresses { get; } = [];

    public List<(int X, int Y, int W, int H)> Masks { get; } = [];

    public int HideCalls { get; private set; }

    public int BannerShown { get; private set; }

    public int BannerHidden { get; private set; }

    public LoginOutcome? FinishedOutcome { get; private set; }

    public string? FinishedError { get; private set; }

    public void OnTaskChanged(string task) => Tasks.Add(task);

    public void OnProgressChanged(string progress) => Progresses.Add(progress);

    public void ShowPrivacyMask(int x, int y, int width, int height) => Masks.Add((x, y, width, height));

    public void HidePrivacyMask() => HideCalls++;

    public void ShowBanner() => BannerShown++;

    public void HideBanner() => BannerHidden++;

    public void OnFinished(LoginOutcome outcome, string? error)
    {
        FinishedOutcome = outcome;
        FinishedError = error;
    }
}

internal sealed class FakeUidCache : ILoginUidCache
{
    public Dictionary<string, string> Values { get; } = new(StringComparer.Ordinal);

    public string? GetLoginUid(string account) => Values.TryGetValue(account, out var value) ? value : null;

    public void SetLoginUid(string account, string uid) => Values[account] = uid;
}

internal sealed class FakeAuthenticator : ISeewoAuthenticator
{
    public SeewoAuthResult Result { get; set; } = SeewoAuthResult.Success(
        new SeewoSession("token", new SeewoUser("uid-1", "user", "nick", "phone"), 0));

    public int Calls { get; private set; }

    public Task<SeewoAuthResult> LoginAsync(string account, string password, CancellationToken cancellationToken)
    {
        Calls++;
        return Task.FromResult(Result);
    }
}

internal sealed class FakeTokenPipe : ISeewoTokenPipe
{
    public Queue<object> Responses { get; } = new();

    public List<TokenDeliveryRequest> Requests { get; } = [];

    public Task<TokenDeliveryResponse> SendTokenAsync(TokenDeliveryRequest request, CancellationToken cancellationToken)
    {
        Requests.Add(request);

        if (Responses.Count == 0)
        {
            return Task.FromResult(new TokenDeliveryResponse(true, "OK", string.Empty, false));
        }

        var next = Responses.Dequeue();
        return next switch
        {
            TokenDeliveryResponse response => Task.FromResult(response),
            Exception exception => Task.FromException<TokenDeliveryResponse>(exception),
            _ => Task.FromResult(new TokenDeliveryResponse(true, "OK", string.Empty, false)),
        };
    }
}

internal sealed class RecordingInputSimulator : IInputSimulator
{
    public List<(int X, int Y)> Clicks { get; } = [];

    public List<(string Text, bool Secret)> Typed { get; } = [];

    public int EnterCalls { get; private set; }

    public Task ClickAsync(int x, int y, CancellationToken cancellationToken)
    {
        Clicks.Add((x, y));
        return Task.CompletedTask;
    }

    public Task TypeTextAsync(string text, bool secret, CancellationToken cancellationToken)
    {
        Typed.Add((text, secret));
        return Task.CompletedTask;
    }

    public Task PressEnterAsync(CancellationToken cancellationToken)
    {
        EnterCalls++;
        return Task.CompletedTask;
    }
}

internal sealed class FixedDisplayMetrics(ScreenMetrics metrics) : IDisplayMetrics
{
    public ScreenMetrics Capture() => metrics;
}

internal static class TestConfigs
{
    public static Config Create()
    {
        var config = new Config();
        config.Login.SkipIfLoggedIn = false;
        config.Login.Timeout.AfterLaunch = 0;
        config.Login.Timeout.LaunchPollingTimeout = 1;
        config.Login.Timeout.LaunchPollingInterval = 0.1;
        config.Login.Timeout.EnterLoginUI = 0;
        config.Login.Timeout.SwitchTab = 0;
        config.App.MaxRetries = 0;
        return config;
    }
}

internal static class TestContexts
{
    public static LoginContext Create(Config config, ILoginPresenter? presenter = null)
        => new("15777420786", "secret", config, presenter ?? new RecordingPresenter());
}

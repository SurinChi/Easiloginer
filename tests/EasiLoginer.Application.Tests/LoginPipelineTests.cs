using Microsoft.Extensions.Logging.Abstractions;
using EasiLoginer.Application.Automation;
using EasiLoginer.Domain.Automation;
using EasiLoginer.Domain.Configuration;
using FluentAssertions;

namespace EasiLoginer.Application.Tests;

/// <summary>
/// 登录管线（文档 §9.1 / §13.2）：成功、可重试、不可重试、取消、跳过、超时。
/// </summary>
public sealed class LoginPipelineTests : IDisposable
{
    private readonly string _root;
    private readonly string _executable;
    private readonly FakeSession _session = new();
    private readonly FakeUidCache _uidCache = new();
    private readonly FakeAuthenticator _authenticator = new();

    public LoginPipelineTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "EasiLoginerTests", "pipeline", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _executable = Path.Combine(_root, "swenlauncher.exe");
        File.WriteAllText(_executable, "stub");
        _session.Path = _executable;
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    /// <summary>准备 → 登录 → 收尾；统计计数正确。</summary>
    [Fact]
    public async Task 成功路径()
    {
        var config = TestConfigs.Create();
        var presenter = new RecordingPresenter();
        var context = TestContexts.Create(config, presenter);
        var strategy = new ScriptedStrategy();

        var outcome = await CreatePipeline().RunAsync(strategy, context, CancellationToken.None);

        outcome.Should().Be(LoginOutcome.Success);
        strategy.BeforePrepareCalls.Should().Be(1);
        strategy.LoginCalls.Should().Be(1);
        strategy.FinishedCalls.Should().Be(1);
        strategy.LastOutcome.Should().Be(LoginOutcome.Success);
        presenter.FinishedOutcome.Should().Be(LoginOutcome.Success);

        config.Statistics.LoginCounts.Should().Be(1);
        config.Statistics.LoginSuccessCounts.Should().Be(1);
        config.Statistics.LoginFailureCounts.Should().Be(0);
        config.Statistics.TotalLoginTime.Should().BeGreaterThanOrEqualTo(0);

        _session.Kills.Should().ContainSingle().Which.Process.Should().Be("EasiNote");
        _session.Launches.Should().ContainSingle().Which.Args.Should().BeEmpty();
    }

    /// <summary>可重试失败后成功：总尝试次数 = 重试次数 + 1。</summary>
    [Fact]
    public async Task 可重试失败后成功()
    {
        var config = TestConfigs.Create();
        config.App.MaxRetries = 2;
        var strategy = new ScriptedStrategy();
        strategy.Failures.Enqueue(new LoginException("临时失败", canRetry: true));
        strategy.Failures.Enqueue(new LoginException("再失败一次", canRetry: true));

        var outcome = await CreatePipeline(retryDelay: TimeSpan.Zero).RunAsync(
            strategy, TestContexts.Create(config), CancellationToken.None);

        outcome.Should().Be(LoginOutcome.Success);
        strategy.LoginCalls.Should().Be(3);
        config.Statistics.LoginSuccessCounts.Should().Be(1);
    }

    /// <summary>重试耗尽后失败，失败数由总数推出。</summary>
    [Fact]
    public async Task 重试耗尽后失败()
    {
        var config = TestConfigs.Create();
        config.App.MaxRetries = 1;
        var strategy = new ScriptedStrategy();
        strategy.Failures.Enqueue(new LoginException("失败", canRetry: true));
        strategy.Failures.Enqueue(new LoginException("失败", canRetry: true));

        var outcome = await CreatePipeline(retryDelay: TimeSpan.Zero).RunAsync(
            strategy, TestContexts.Create(config), CancellationToken.None);

        outcome.Should().Be(LoginOutcome.Failed);
        strategy.LoginCalls.Should().Be(2);
        strategy.LastError.Should().Be("失败");

        config.Statistics.LoginCounts.Should().Be(1);
        config.Statistics.LoginFailureCounts.Should().Be(1);
        config.Statistics.LoginSuccessCounts.Should().Be(0);
    }

    /// <summary>不可重试错误立即结束，不消耗重试次数。</summary>
    [Fact]
    public async Task 不可重试错误立即结束()
    {
        var config = TestConfigs.Create();
        config.App.MaxRetries = 3;
        var strategy = new ScriptedStrategy();
        strategy.Failures.Enqueue(new LoginException("致命错误", canRetry: false));

        var outcome = await CreatePipeline().RunAsync(strategy, TestContexts.Create(config), CancellationToken.None);

        outcome.Should().Be(LoginOutcome.Failed);
        strategy.LoginCalls.Should().Be(1);
    }

    /// <summary>取消映射为 Cancelled，计入中断而不是失败。</summary>
    [Fact]
    public async Task 取消不计失败()
    {
        var config = TestConfigs.Create();
        var strategy = new ScriptedStrategy();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var outcome = await CreatePipeline().RunAsync(strategy, TestContexts.Create(config), cancellation.Token);

        outcome.Should().Be(LoginOutcome.Cancelled);
        strategy.LoginCalls.Should().Be(0);
        config.Statistics.LoginInterruptCounts.Should().Be(1);
        config.Statistics.LoginFailureCounts.Should().Be(0);
    }

    /// <summary>目标账号已登录：跳过且不重启希沃。</summary>
    [Fact]
    public async Task 已登录时跳过()
    {
        var config = TestConfigs.Create();
        config.Login.SkipIfLoggedIn = true;
        _session.Patched = true;
        _session.LoginState = new EasiNoteLoginState(202, "uid-1", "token");
        _uidCache.Values["15777420786"] = "uid-1";

        var outcome = await CreatePipeline().RunAsync(new ScriptedStrategy(), TestContexts.Create(config), CancellationToken.None);

        outcome.Should().Be(LoginOutcome.Cancelled);
        _session.Kills.Should().BeEmpty("已登录时不应关闭希沃白板");
        _session.Launches.Should().BeEmpty();
        config.Statistics.LoginInterruptCounts.Should().Be(1);
    }

    /// <summary>无 login_uid 缓存时联网解析并写回缓存。</summary>
    [Fact]
    public async Task 无缓存时联网解析并缓存uid()
    {
        var config = TestConfigs.Create();
        config.Login.SkipIfLoggedIn = true;
        _session.Patched = true;
        _session.LoginState = new EasiNoteLoginState(202, "uid-1", "token");
        _authenticator.Result = SeewoAuthResult.Success(new SeewoSession("t", new SeewoUser("uid-1", "u", "n", "p"), 0));

        var outcome = await CreatePipeline().RunAsync(new ScriptedStrategy(), TestContexts.Create(config), CancellationToken.None);

        outcome.Should().Be(LoginOutcome.Cancelled);
        _authenticator.Calls.Should().Be(1);
        _uidCache.Values["15777420786"].Should().Be("uid-1");
    }

    /// <summary>启动器缺失时不可重试，且不得先终止用户的希沃白板。</summary>
    [Fact]
    public async Task 启动器缺失时不终止进程()
    {
        _session.Path = null;

        var outcome = await CreatePipeline().RunAsync(new ScriptedStrategy(), TestContexts.Create(TestConfigs.Create()), CancellationToken.None);

        outcome.Should().Be(LoginOutcome.Failed);
        _session.Kills.Should().BeEmpty();
        _session.Launches.Should().BeEmpty();
    }

    /// <summary>窗口等待超时按可重试处理。</summary>
    [Fact]
    public async Task 窗口超时可重试()
    {
        var config = TestConfigs.Create();
        config.App.MaxRetries = 1;
        _session.WindowHandle = 0;

        var outcome = await CreatePipeline(retryDelay: TimeSpan.Zero).RunAsync(
            new ScriptedStrategy(), TestContexts.Create(config), CancellationToken.None);

        outcome.Should().Be(LoginOutcome.Failed);
        _session.WaitCalls.Should().Be(2, "第一次失败后应重试一次");
    }

    /// <summary>收尾回调抛异常不能影响最终结果。</summary>
    [Fact]
    public async Task 收尾异常被吞掉()
    {
        var config = TestConfigs.Create();
        var strategy = new ThrowingFinishStrategy();

        var outcome = await CreatePipeline().RunAsync(strategy, TestContexts.Create(config), CancellationToken.None);

        outcome.Should().Be(LoginOutcome.Success);
    }

    /// <summary>相同进度文案只上报一次。</summary>
    [Fact]
    public async Task 进度文案去重()
    {
        var presenter = new RecordingPresenter();
        var context = TestContexts.Create(TestConfigs.Create(), presenter);
        var strategy = new RepeatedProgressStrategy();

        await CreatePipeline().RunAsync(strategy, context, CancellationToken.None);

        presenter.Progresses.Count(progress => progress == "同一条进度").Should().Be(1, "相同进度文案只上报一次");
    }

    /// <summary>启用横幅时：登录开始显示、收尾隐藏。</summary>
    [Fact]
    public async Task 横幅在登录期间显示并在收尾隐藏()
    {
        var config = TestConfigs.Create();
        var presenter = new RecordingPresenter();

        await CreatePipeline().RunAsync(new ScriptedStrategy(), TestContexts.Create(config, presenter), CancellationToken.None);

        presenter.BannerShown.Should().Be(1);
        presenter.BannerHidden.Should().Be(1);
    }

    /// <summary>关闭横幅时完全不动它。</summary>
    [Fact]
    public async Task 关闭横幅时不显示()
    {
        var config = TestConfigs.Create();
        config.Banner.Enabled = false;
        var presenter = new RecordingPresenter();

        await CreatePipeline().RunAsync(new ScriptedStrategy(), TestContexts.Create(config, presenter), CancellationToken.None);

        presenter.BannerShown.Should().Be(0);
    }

    private LoginPipeline CreatePipeline(TimeSpan? retryDelay = null)
        => new(
            _session,
            _uidCache,
            _authenticator,
            NullLogger<LoginPipeline>.Instance,
            retryDelay ?? TimeSpan.Zero,
            (_, _) => Task.CompletedTask);

    private sealed class ThrowingFinishStrategy : ILoginStrategy
    {
        public string Id => "throwing";

        public string ResolveLaunchArgs(LoginContext context) => string.Empty;

        public Task BeforePrepareAsync(LoginContext context, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task LoginAsync(LoginContext context, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task OnFinishedAsync(LoginContext context, LoginOutcome outcome, string? error, CancellationToken cancellationToken)
            => throw new InvalidOperationException("收尾炸了");
    }

    private sealed class RepeatedProgressStrategy : ILoginStrategy
    {
        public string Id => "repeat";

        public string ResolveLaunchArgs(LoginContext context) => string.Empty;

        public Task BeforePrepareAsync(LoginContext context, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task LoginAsync(LoginContext context, CancellationToken cancellationToken)
        {
            context.ReportProgress("同一条进度");
            context.ReportProgress("同一条进度");
            return Task.CompletedTask;
        }

        public Task OnFinishedAsync(LoginContext context, LoginOutcome outcome, string? error, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}

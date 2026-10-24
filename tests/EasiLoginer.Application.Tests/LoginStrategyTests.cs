using EasiLoginer.Application.Automation;
using EasiLoginer.Automation.Strategies;
using EasiLoginer.Domain.Automation;
using EasiLoginer.Domain.Geometry;
using FluentAssertions;

namespace EasiLoginer.Application.Tests;

/// <summary>令牌投递策略（文档 §9.5）。</summary>
public sealed class TokenLoginStrategyTests
{
    private readonly FakeSession _session = new() { Patched = true };
    private readonly FakeAuthenticator _authenticator = new();
    private readonly FakeTokenPipe _pipe = new();

    /// <summary>未修补时不可重试地失败，且不发起 API 登录。</summary>
    [Fact]
    public async Task 未修补时失败()
    {
        _session.Patched = false;

        var exception = await Assert.ThrowsAsync<LoginException>(
            () => CreateStrategy().BeforePrepareAsync(CreateContext(), CancellationToken.None));

        exception.CanRetry.Should().BeFalse();
        _authenticator.Calls.Should().Be(0);
    }

    /// <summary>网络异常可重试。</summary>
    [Fact]
    public async Task 网络异常可重试()
    {
        _authenticator.Result = SeewoAuthResult.Failed(SeewoAuthFailure.Network, "boom");

        var exception = await Assert.ThrowsAsync<LoginException>(
            () => CreateStrategy().BeforePrepareAsync(CreateContext(), CancellationToken.None));

        exception.CanRetry.Should().BeTrue();
    }

    /// <summary>凭据错误与风控都不可重试。</summary>
    [Theory]
    [InlineData(SeewoAuthFailure.Credentials, "账号或密码错误")]
    [InlineData(SeewoAuthFailure.Captcha, "账号被风控")]
    [InlineData(SeewoAuthFailure.Unknown, "未知登录异常")]
    public async Task 认证失败不可重试(SeewoAuthFailure failure, string message)
    {
        _authenticator.Result = SeewoAuthResult.Failed(failure, message);

        var exception = await Assert.ThrowsAsync<LoginException>(
            () => CreateStrategy().BeforePrepareAsync(CreateContext(), CancellationToken.None));

        exception.CanRetry.Should().BeFalse();
        exception.Message.Should().Be(message);
    }

    /// <summary>成功：载荷字段来自 API 结果。</summary>
    [Fact]
    public async Task 投递载荷来自认证结果()
    {
        _authenticator.Result = SeewoAuthResult.Success(
            new SeewoSession("token-abc", new SeewoUser("uid-1", "user", "nick", "13800000000"), 0));

        var context = CreateContext();
        var strategy = CreateStrategy();
        await strategy.BeforePrepareAsync(context, CancellationToken.None);
        await strategy.LoginAsync(context, CancellationToken.None);

        var request = _pipe.Requests.Should().ContainSingle().Subject;
        request.Token.Should().Be("token-abc");
        request.UserId.Should().Be("uid-1");
        request.UserName.Should().Be("user");
        request.NickName.Should().Be("nick");
        request.Phone.Should().Be("13800000000");
        context.ReportProgress("登录完成");
    }

    /// <summary>回执令牌失效：不可重试。</summary>
    [Fact]
    public async Task 令牌失效不可重试()
    {
        _pipe.Responses.Enqueue(new TokenDeliveryResponse(false, "令牌失效", "4000", TokenExpired: true));

        var exception = await Assert.ThrowsAsync<LoginException>(() => RunLoginAsync());

        exception.CanRetry.Should().BeFalse();
        exception.Message.Should().Contain("令牌已失效");
    }

    /// <summary>回执失败但非过期：可重试。</summary>
    [Fact]
    public async Task 回执失败可重试()
    {
        _pipe.Responses.Enqueue(new TokenDeliveryResponse(false, "服务忙", string.Empty, TokenExpired: false));

        var exception = await Assert.ThrowsAsync<LoginException>(() => RunLoginAsync());

        exception.CanRetry.Should().BeTrue();
    }

    /// <summary>管道权限被拒绝：不可重试。</summary>
    [Fact]
    public async Task 管道被拒绝不可重试()
    {
        _pipe.Responses.Enqueue(new TokenPipeDeniedException("denied"));

        var exception = await Assert.ThrowsAsync<LoginException>(() => RunLoginAsync());

        exception.CanRetry.Should().BeFalse();
        exception.Message.Should().Contain("权限不足");
    }

    /// <summary>管道两次都不可用：不可重试地失败。</summary>
    [Fact]
    public async Task 管道始终不可用()
    {
        _pipe.Responses.Enqueue(new TokenPipeUnavailableException("no pipe"));
        _pipe.Responses.Enqueue(new TokenPipeUnavailableException("no pipe"));

        var exception = await Assert.ThrowsAsync<LoginException>(() => RunLoginAsync());

        exception.CanRetry.Should().BeFalse();
        exception.Message.Should().Contain("登录管道未启动");
        _pipe.Requests.Should().HaveCount(2);
    }

    /// <summary>第一次管道未就绪、第二次成功。</summary>
    [Fact]
    public async Task 管道第二次就绪()
    {
        _pipe.Responses.Enqueue(new TokenPipeUnavailableException("no pipe"));
        _pipe.Responses.Enqueue(new TokenDeliveryResponse(true, "OK", string.Empty, false));

        await RunLoginAsync();

        _pipe.Requests.Should().HaveCount(2);
    }

    private async Task RunLoginAsync()
    {
        var context = CreateContext();
        var strategy = CreateStrategy();
        await strategy.BeforePrepareAsync(context, CancellationToken.None);
        await strategy.LoginAsync(context, CancellationToken.None);
    }

    private TokenLoginStrategy CreateStrategy()
        => new(_authenticator, _pipe, _session, (_, _) => Task.CompletedTask);

    private static LoginContext CreateContext() => TestContexts.Create(TestConfigs.Create());
}

/// <summary>固定位置策略（文档 §9.2）。</summary>
public sealed class FixedPositionStrategyTests
{
    private readonly FakeSession _session = new();
    private readonly RecordingInputSimulator _input = new();

    /// <summary>非 IWB 且未显式指定时，强制 <c>-m Display iwb</c>。</summary>
    [Fact]
    public void 强制白板启动参数()
    {
        _session.IsIwb = false;

        CreateStrategy(new ScreenMetrics(new PixelPoint(1920, 1080), 1.0, 60))
            .ResolveLaunchArgs(TestContexts.Create(TestConfigs.Create()))
            .Should().Be("-m Display iwb");
    }

    /// <summary>本机就是 IWB 或用户显式指定 <c>-m display</c> 时沿用用户参数；<c>-m edit</c> 仍会被强制成 iwb。</summary>
    [Theory]
    [InlineData("-m Display iwb", false, "-m Display iwb")]
    [InlineData("-m display", false, "-m display")]
    [InlineData("--mode Display", false, "--mode Display")]
    [InlineData("", true, "")]
    [InlineData("-m edit", true, "-m Display iwb")]
    public void 沿用用户启动参数(string args, bool isIwb, string expected)
    {
        _session.IsIwb = isIwb;
        var config = TestConfigs.Create();
        config.Login.EasiNote.Args = args;

        var resolved = CreateStrategy(new ScreenMetrics(new PixelPoint(1920, 1080), 1.0, 60))
            .ResolveLaunchArgs(TestContexts.Create(config));

        resolved.Should().Be(expected);
    }

    /// <summary>100% 缩放时按配置坐标原样点击，并输入账号密码、回车提交。</summary>
    [Fact]
    public async Task 一百缩放的点击序列()
    {
        var config = TestConfigs.Create();
        var context = TestContexts.Create(config);

        await CreateStrategy(new ScreenMetrics(new PixelPoint(1920, 1080), 1.0, 60))
            .LoginAsync(context, CancellationToken.None);

        _input.Clicks.Should().Equal(
            (172, 1044),
            (1090, 350),
            (1000, 420),
            (1000, 490),
            (935, 724));

        _input.Typed.Should().Equal(
            ("15777420786", false),
            ("secret", true));

        _input.EnterCalls.Should().Be(1);
    }

    /// <summary>125% 缩放时按上游公式换算（含逐步截断）。</summary>
    [Fact]
    public async Task 一百二十五缩放的点击序列()
    {
        var context = TestContexts.Create(TestConfigs.Create());

        await CreateStrategy(new ScreenMetrics(new PixelPoint(1920, 1080), 1.25, 60))
            .LoginAsync(context, CancellationToken.None);

        _input.Clicks.Should().Equal(
            (215, 1305),
            (1362, 437),
            (1250, 524),
            (1250, 612),
            (1168, 904));
    }

    /// <summary>关闭智能缩放时整条链路不缩放。</summary>
    [Fact]
    public async Task 关闭缩放时使用原坐标()
    {
        var config = TestConfigs.Create();
        config.Login.Position.EnableScaling = false;

        await CreateStrategy(new ScreenMetrics(new PixelPoint(1920, 1080), 1.25, 60))
            .LoginAsync(TestContexts.Create(config), CancellationToken.None);

        _input.Clicks.Should().Equal(
            (172, 1044),
            (1090, 350),
            (1000, 420),
            (1000, 490),
            (935, 724));
    }

    /// <summary>隐私遮罩位置随缩放换算，尺寸始终按缩放缩放（与上游一致）。</summary>
    [Fact]
    public async Task 隐私遮罩按缩放显示()
    {
        var presenter = new RecordingPresenter();
        var context = TestContexts.Create(TestConfigs.Create(), presenter);

        await CreateStrategy(new ScreenMetrics(new PixelPoint(1920, 1080), 1.25, 60))
            .LoginAsync(context, CancellationToken.None);

        presenter.Masks.Should().ContainSingle().Which.Should().Be((1085, 476, 550, 482));
    }

    /// <summary>关闭遮罩时不显示。</summary>
    [Fact]
    public async Task 关闭遮罩时不显示()
    {
        var presenter = new RecordingPresenter();
        var config = TestConfigs.Create();
        config.Experimental.PrivacyMask.Enabled = false;

        await CreateStrategy(new ScreenMetrics(new PixelPoint(1920, 1080), 1.0, 60))
            .LoginAsync(TestContexts.Create(config, presenter), CancellationToken.None);

        presenter.Masks.Should().BeEmpty();
    }

    /// <summary>已取消时立即停止，不再产生点击。</summary>
    [Fact]
    public async Task 取消后不再点击()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var action = () => CreateStrategy(new ScreenMetrics(new PixelPoint(1920, 1080), 1.0, 60))
            .LoginAsync(TestContexts.Create(TestConfigs.Create()), cancellation.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
        _input.Clicks.Should().BeEmpty();
    }

    private FixedPositionStrategy CreateStrategy(ScreenMetrics metrics)
        => new(_input, _session, new FixedDisplayMetrics(metrics), (_, _) => Task.CompletedTask);
}

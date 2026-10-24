using Microsoft.Extensions.Logging.Abstractions;
using EasiLoginer.Application.Automation;
using EasiLoginer.Domain.Configuration;
using FluentAssertions;

namespace EasiLoginer.Application.Tests;

/// <summary>登录前置：跳过一次与运行前确认（文档 §2.1 / §6.2）。</summary>
public sealed class LoginPreflightTests
{
    /// <summary>命中 SkipOnce：清除标记、要求落盘、本次跳过。</summary>
    [Fact]
    public async Task 跳过一次()
    {
        var config = new Config();
        config.Login.SkipOnce = true;

        var result = await CreatePreflight().RunAsync(config, null, "u", manual: false, CancellationToken.None);

        result.Outcome.Should().Be(PreflightOutcome.SkippedOnce);
        result.ConfigChanged.Should().BeTrue();
        config.Login.SkipOnce.Should().BeFalse("标记必须被消费掉，否则永远登录不了");
    }

    /// <summary>关闭警告弹窗时直接继续，不调用确认通道。</summary>
    [Fact]
    public async Task 关闭警告时直接继续()
    {
        var config = new Config();
        config.Warning.Enabled = false;
        var confirmation = new FakeConfirmation();

        var result = await CreatePreflight().RunAsync(config, confirmation, "u", manual: false, CancellationToken.None);

        result.Outcome.Should().Be(PreflightOutcome.Proceed);
        confirmation.Calls.Should().Be(0);
    }

    /// <summary><c>--manual</c> 跳过确认弹窗（只影响确认，不改变其它行为）。</summary>
    [Fact]
    public async Task 手动模式跳过确认()
    {
        var config = new Config();
        var confirmation = new FakeConfirmation(LoginConfirmationChoice.Cancel);

        var result = await CreatePreflight().RunAsync(config, confirmation, "u", manual: true, CancellationToken.None);

        result.Outcome.Should().Be(PreflightOutcome.Proceed);
        confirmation.Calls.Should().Be(0);
    }

    /// <summary>无确认界面时记警告并继续。</summary>
    [Fact]
    public async Task 无界面时继续()
    {
        var result = await CreatePreflight().RunAsync(new Config(), null, "u", manual: false, CancellationToken.None);

        result.Outcome.Should().Be(PreflightOutcome.Proceed);
    }

    /// <summary>用户选择立即执行。</summary>
    [Fact]
    public async Task 立即执行()
    {
        var confirmation = new FakeConfirmation(LoginConfirmationChoice.Proceed);

        var result = await CreatePreflight().RunAsync(new Config(), confirmation, "u", manual: false, CancellationToken.None);

        result.Outcome.Should().Be(PreflightOutcome.Proceed);
        confirmation.Calls.Should().Be(1);
    }

    /// <summary>用户取消 → 中止。</summary>
    [Fact]
    public async Task 用户取消()
    {
        var confirmation = new FakeConfirmation(LoginConfirmationChoice.Cancel);

        var result = await CreatePreflight().RunAsync(new Config(), confirmation, "u", manual: false, CancellationToken.None);

        result.Outcome.Should().Be(PreflightOutcome.CancelledByUser);
    }

    /// <summary>推迟一次后再问，第二次选择执行。</summary>
    [Fact]
    public async Task 推迟后再问()
    {
        var config = new Config();
        config.Warning.MaxDelays = 1;
        config.Warning.DelayTime = 5;
        var confirmation = new FakeConfirmation(LoginConfirmationChoice.Delay, LoginConfirmationChoice.Proceed);

        var delayCalls = 0;
        var result = await CreatePreflight(() =>
            {
                delayCalls++;
                return Task.CompletedTask;
            })
            .RunAsync(config, confirmation, "u", manual: false, CancellationToken.None);

        result.Outcome.Should().Be(PreflightOutcome.Proceed);
        confirmation.Calls.Should().Be(2);
        delayCalls.Should().Be(1, "推迟一次应等待一次 DelayTime");
    }

    /// <summary>推迟次数用完后继续登录，不再等待。</summary>
    [Fact]
    public async Task 推迟次数用完后继续()
    {
        var config = new Config();
        config.Warning.MaxDelays = 0;
        var confirmation = new FakeConfirmation(LoginConfirmationChoice.Delay);
        var delayCalls = 0;

        var result = await CreatePreflight(() =>
            {
                delayCalls++;
                return Task.CompletedTask;
            })
            .RunAsync(config, confirmation, "u", manual: false, CancellationToken.None);

        result.Outcome.Should().Be(PreflightOutcome.Proceed);
        delayCalls.Should().Be(0);
    }

    private static LoginPreflight CreatePreflight(Func<Task>? onDelay = null)
        => new(
            NullLogger<LoginPreflight>.Instance,
            (_, _) => onDelay?.Invoke() ?? Task.CompletedTask);

    private sealed class FakeConfirmation(params LoginConfirmationChoice[] choices) : ILoginConfirmation
    {
        private readonly Queue<LoginConfirmationChoice> _choices = new(choices);

        public int Calls { get; private set; }

        public Task<LoginConfirmationChoice> ConfirmAsync(LoginConfirmationRequest request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(_choices.Count > 0 ? _choices.Dequeue() : LoginConfirmationChoice.Proceed);
        }
    }
}

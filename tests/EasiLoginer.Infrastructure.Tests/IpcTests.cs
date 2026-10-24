using EasiLoginer.Infrastructure.Ipc;
using FluentAssertions;

namespace EasiLoginer.Infrastructure.Tests;

/// <summary>单实例与 argv 转发的行为测试。</summary>
public sealed class IpcTests
{
    /// <summary>同一互斥体名只能有一个主实例。</summary>
    [Fact]
    public void 互斥体判定主次实例()
    {
        var name = "EasiLoginerMutex_Test_" + Guid.NewGuid().ToString("N");

        using var first = new SingleInstanceService();
        using var second = new SingleInstanceService();

        first.TryAcquire(name).Should().BeTrue();
        second.TryAcquire(name).Should().BeFalse();
    }

    /// <summary>服务端不在时，转发必须快速失败而不是卡住。</summary>
    [Fact]
    public async Task 无主实例时转发失败()
    {
        var pipeName = "EasiLoginer_Test_" + Guid.NewGuid().ToString("N");
        var frame = new ArgvFrame(["EasiLoginer.exe", "login"]);

        var sent = await ArgvPipeClient.TrySendAsync(
            pipeName, frame, TimeSpan.FromMilliseconds(200), CancellationToken.None);

        sent.Should().BeFalse();
    }

    /// <summary>完整往返：客户端写入的帧被服务端按原样接收。</summary>
    [Fact]
    public async Task 管道转发往返成功()
    {
        var pipeName = "EasiLoginer_Test_" + Guid.NewGuid().ToString("N");
        var received = new TaskCompletionSource<ArgvFrame>(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var server = new ArgvPipeServer(pipeName, (frame, _) =>
        {
            received.TrySetResult(frame);
            return Task.CompletedTask;
        });
        server.Start();

        var sent = await ArgvPipeClient.TrySendAsync(
            pipeName,
            new ArgvFrame(["EasiLoginer.exe", "login", "-i", "profile-1"]),
            TimeSpan.FromSeconds(5),
            CancellationToken.None);

        sent.Should().BeTrue();

        var frame = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        frame.Argv.Should().Equal("EasiLoginer.exe", "login", "-i", "profile-1");
    }
}

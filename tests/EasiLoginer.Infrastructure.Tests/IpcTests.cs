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

    /// <summary>
    /// 处理回调抛异常时监听循环必须继续：否则一次坏命令会让单实例转发永久失效。
    /// </summary>
    [Fact]
    public async Task 回调异常不终止监听()
    {
        var pipeName = "EasiLoginer_Test_" + Guid.NewGuid().ToString("N");
        var calls = 0;
        var errors = 0;
        var second = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var server = new ArgvPipeServer(
            pipeName,
            (_, _) =>
            {
                if (Interlocked.Increment(ref calls) == 1)
                {
                    throw new InvalidOperationException("模拟一次命令失败");
                }

                second.TrySetResult();
                return Task.CompletedTask;
            },
            _ => Interlocked.Increment(ref errors));
        server.Start();

        var firstSent = await ArgvPipeClient.TrySendAsync(
            pipeName, new ArgvFrame(["EasiLoginer.exe", "settings"]), TimeSpan.FromSeconds(5), CancellationToken.None);
        var secondSent = await ArgvPipeClient.TrySendAsync(
            pipeName, new ArgvFrame(["EasiLoginer.exe", "settings"]), TimeSpan.FromSeconds(5), CancellationToken.None);

        firstSent.Should().BeTrue();
        secondSent.Should().BeTrue();
        await second.Task.WaitAsync(TimeSpan.FromSeconds(5));
        errors.Should().Be(1);
    }

    /// <summary>argv 为 null 的非法帧必须被丢弃，不能把服务端循环打断（NullReferenceException）。</summary>
    [Fact]
    public async Task 空argv的帧被忽略()
    {
        var pipeName = "EasiLoginer_Test_" + Guid.NewGuid().ToString("N");
        var calls = 0;
        var received = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var server = new ArgvPipeServer(pipeName, (_, _) =>
        {
            Interlocked.Increment(ref calls);
            received.TrySetResult();
            return Task.CompletedTask;
        });
        server.Start();

        // 直接写一个畸形帧
        await using (var client = new System.IO.Pipes.NamedPipeClientStream(
            ".", pipeName, System.IO.Pipes.PipeDirection.Out, System.IO.Pipes.PipeOptions.Asynchronous))
        {
            await client.ConnectAsync(5000);
            await System.Text.Json.JsonSerializer.SerializeAsync(client, new { argv = (string[]?)null });
        }

        var sent = await ArgvPipeClient.TrySendAsync(
            pipeName, new ArgvFrame(["EasiLoginer.exe", "skip"]), TimeSpan.FromSeconds(5), CancellationToken.None);

        sent.Should().BeTrue();
        await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        calls.Should().Be(1, "非法帧不应进入业务回调");
    }
}

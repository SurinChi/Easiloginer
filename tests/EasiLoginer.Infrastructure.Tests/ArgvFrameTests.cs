using System.Text.Json;
using EasiLoginer.Infrastructure.Ipc;
using FluentAssertions;

namespace EasiLoginer.Infrastructure.Tests;

/// <summary>argv 帧的线上格式必须与上游一致（文档 §7.4）。</summary>
public sealed class ArgvFrameTests
{
    /// <summary>字段名固定为 argv，且不依赖命名策略。</summary>
    [Fact]
    public void 序列化为固定结构()
    {
        var frame = new ArgvFrame(["EasiLoginer.exe", "login", "-i", "abc"]);

        var json = JsonSerializer.Serialize(frame, IpcJsonContext.Default.ArgvFrame);

        json.Should().Be("""{"argv":["EasiLoginer.exe","login","-i","abc"]}""");
    }

    /// <summary>反序列化后内容一致，中文路径不受影响。</summary>
    [Fact]
    public void 反序列化保留内容()
    {
        const string json = """{"argv":["EasiLoginer.exe","login","-a","账号","-p","密码"]}""";

        var frame = JsonSerializer.Deserialize(json, IpcJsonContext.Default.ArgvFrame);

        frame.Should().NotBeNull();
        frame!.Argv.Should().Equal("EasiLoginer.exe", "login", "-a", "账号", "-p", "密码");
    }

    /// <summary>空 argv 也必须是合法帧（例如仅带 exe 名的转发）。</summary>
    [Fact]
    public void 空argv可往返()
    {
        var frame = new ArgvFrame([]);

        var json = JsonSerializer.Serialize(frame, IpcJsonContext.Default.ArgvFrame);
        var roundTrip = JsonSerializer.Deserialize(json, IpcJsonContext.Default.ArgvFrame);

        roundTrip!.Argv.Should().BeEmpty();
    }
}

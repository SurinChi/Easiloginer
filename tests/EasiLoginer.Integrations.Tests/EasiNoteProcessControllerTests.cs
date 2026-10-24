using Microsoft.Extensions.Logging.Abstractions;
using EasiLoginer.Application.Abstractions;
using EasiLoginer.Integrations.EasiNote.Process;
using FluentAssertions;

namespace EasiLoginer.Integrations.Tests;

/// <summary>希沃进程与窗口控制（文档 §8.6）。</summary>
public sealed class EasiNoteProcessControllerTests
{
    /// <summary>默认只终止希沃主进程。</summary>
    [Fact]
    public void 默认只终止主进程()
    {
        var processes = new FakeProcessManager();
        var controller = Create(processes);

        controller.KillTargets("EasiNote", killAgent: false, extraKills: string.Empty, timeoutSeconds: 2.5);

        processes.Kills.Should().ContainSingle()
            .Which.Should().Be(("EasiNote", true, true, 2.5));
    }

    /// <summary><c>KillAgent</c> 时一并终止 EasiAgent。</summary>
    [Fact]
    public void 可选终止EasiAgent()
    {
        var processes = new FakeProcessManager();
        var controller = Create(processes);

        controller.KillTargets("EasiNote", killAgent: true, extraKills: string.Empty, timeoutSeconds: 1);

        processes.Kills.Select(kill => kill.Name).Should().Equal("EasiNote", "EasiAgent");
    }

    /// <summary>额外进程按英文逗号分割、去空白，并去掉小写 <c>.exe</c> 后缀。</summary>
    [Fact]
    public void 额外进程被拆分与规范化()
    {
        var processes = new FakeProcessManager();
        var controller = Create(processes);

        controller.KillTargets("EasiNote", killAgent: false, extraKills: " Extra1 , Extra2.exe ,Extra3", timeoutSeconds: 1);

        processes.Kills.Select(kill => kill.Name).Should().Equal("EasiNote", "Extra1", "Extra2", "Extra3");
    }

    /// <summary>后缀大小写敏感：上游 <c>removesuffix(".exe")</c> 不会去掉大写的 <c>.EXE</c>。</summary>
    [Fact]
    public void 大写后缀不被去掉()
    {
        var processes = new FakeProcessManager();
        var controller = Create(processes);

        controller.KillTargets("EasiNote", killAgent: false, extraKills: "Tool.EXE", timeoutSeconds: 1);

        processes.Kills.Select(kill => kill.Name).Should().Equal("EasiNote", "Tool.EXE");
    }

    /// <summary>空条目被忽略，不会去终止一个空名字。</summary>
    [Fact]
    public void 空条目被忽略()
    {
        var processes = new FakeProcessManager();
        var controller = Create(processes);

        controller.KillTargets("EasiNote", killAgent: false, extraKills: " , ,", timeoutSeconds: 1);

        processes.Kills.Should().ContainSingle();
    }

    /// <summary>启动参数按空白分割，且不给 shell。</summary>
    [Fact]
    public void 启动参数按空白分割()
    {
        var processes = new FakeProcessManager();
        var controller = Create(processes);
        var exe = Path.Combine(Path.GetTempPath(), "swenlauncher.exe");

        controller.Launch(exe, "  -m   Display iwb ");

        var start = processes.Starts.Should().ContainSingle().Subject;
        start.Path.Should().Be(Path.GetFullPath(exe));
        start.Arguments.Should().Equal("-m", "Display", "iwb");
    }

    /// <summary>空参数不产生任何参数项。</summary>
    [Fact]
    public void 空参数不产生参数项()
    {
        var processes = new FakeProcessManager();
        var controller = Create(processes);

        controller.Launch(Path.Combine(Path.GetTempPath(), "a.exe"), "   ");

        processes.Starts.Should().ContainSingle().Which.Arguments.Should().BeEmpty();
    }

    /// <summary>轮询到窗口后立即返回句柄。</summary>
    [Fact]
    public async Task 等待窗口成功()
    {
        var windows = new FakeWindowService(0, 0, 42);
        var controller = Create(new FakeProcessManager(), windows);

        var handle = await controller.WaitForWindowAsync(
            "希沃白板", false, TimeSpan.FromSeconds(2), TimeSpan.FromMilliseconds(10), CancellationToken.None);

        handle.Should().Be(42);
        windows.FindCalls.Should().Be(3);
    }

    /// <summary>超时返回 0（上游同样以 0 表示未找到）。</summary>
    [Fact]
    public async Task 等待窗口超时返回零()
    {
        var controller = Create(new FakeProcessManager(), new FakeWindowService());

        var handle = await controller.WaitForWindowAsync(
            "希沃白板", false, TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(10), CancellationToken.None);

        handle.Should().Be(0);
    }

    /// <summary>取消时抛 <see cref="OperationCanceledException"/>，让管线识别为用户停止。</summary>
    [Fact]
    public async Task 等待窗口可被取消()
    {
        var controller = Create(new FakeProcessManager(), new FakeWindowService());
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var action = () => controller.WaitForWindowAsync(
            "希沃白板", false, TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(10), cancellation.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    /// <summary>前台化失败只返回 false，不抛异常（上游只记警告）。</summary>
    [Fact]
    public void 前台化失败不抛异常()
    {
        var windows = new FakeWindowService { SwitchResult = false };
        var controller = Create(new FakeProcessManager(), windows);

        var ok = controller.BringToFront(123, pressKey: true);

        ok.Should().BeFalse();
        windows.LastSwitchPressKey.Should().BeTrue();
    }

    private static EasiNoteProcessController Create(FakeProcessManager processes, FakeWindowService? windows = null)
        => new(processes, windows ?? new FakeWindowService(), NullLogger<EasiNoteProcessController>.Instance);

    private sealed class FakeProcessManager : IProcessManager
    {
        public List<(string Name, bool Force, bool Wait, double Timeout)> Kills { get; } = [];

        public List<(string Path, IReadOnlyList<string> Arguments)> Starts { get; } = [];

        public void Kill(string processName, bool force = true, bool wait = true, double timeoutSeconds = 1.0)
            => Kills.Add((processName, force, wait, timeoutSeconds));

        public void Start(string executablePath, IReadOnlyList<string> arguments)
            => Starts.Add((executablePath, arguments));
    }

    private sealed class FakeWindowService(params long[] handles) : IWindowService
    {
        private readonly Queue<long> _handles = new(handles);

        public int FindCalls { get; private set; }

        public bool SwitchResult { get; init; } = true;

        public bool? LastSwitchPressKey { get; private set; }

        public long FindWindow(string title, bool substring = false)
        {
            FindCalls++;
            return _handles.Count > 0 ? _handles.Dequeue() : 0;
        }

        public bool SwitchToWindow(long handle, bool pressKey = false)
        {
            LastSwitchPressKey = pressKey;
            return SwitchResult;
        }

        public IReadOnlyList<WindowInfo> EnumerateWindows() => [];

        public long GetForegroundWindow() => 0;
    }
}

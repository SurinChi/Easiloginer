using Microsoft.Extensions.Logging.Abstractions;
using EasiLoginer.Automation.Input;
using EasiLoginer.Domain.Geometry;
using FluentAssertions;

namespace EasiLoginer.Application.Tests;

/// <summary>输入模拟（文档 §9.3）。用记录型后端验证序列，不触碰真实键鼠。</summary>
public sealed class SendInputSimulatorTests
{
    private static readonly ScreenMetrics FullHd = new(new PixelPoint(1920, 1080), 1.0, 60);

    /// <summary>点击只传递坐标，不做额外换算。</summary>
    [Fact]
    public async Task 点击传递坐标()
    {
        var backend = new RecordingBackend();

        await Create(backend, FullHd).ClickAsync(1090, 350, CancellationToken.None);

        backend.Events.Should().Equal("click(1090,350)");
    }

    /// <summary>普通输入：先清空，再逐字符发送，末尾无剪贴板操作。</summary>
    [Fact]
    public async Task 逐字符输入()
    {
        var backend = new RecordingBackend();

        await Create(backend, FullHd).TypeTextAsync("ab", secret: false, CancellationToken.None);

        backend.Events.Should().Equal(
            "ctrl(A)",
            "key(Back,down)",
            "key(Back,up)",
            "char(a)",
            "char(b)");
    }

    /// <summary>兼容模式：写剪贴板后 Ctrl+V，不逐字符发送。</summary>
    [Fact]
    public async Task 兼容模式使用剪贴板()
    {
        var backend = new RecordingBackend();

        await Create(backend, FullHd, forceCompatibility: true).TypeTextAsync("中文密码", secret: true, CancellationToken.None);

        backend.Events.Should().Equal(
            "ctrl(A)",
            "key(Back,down)",
            "key(Back,up)",
            "clipboard(中文密码)",
            "ctrl(V)");
    }

    /// <summary>物理高度 / 缩放 &lt; 720 时自动进入兼容模式。</summary>
    [Theory]
    [InlineData(1920, 1080, 1.0, false)]
    [InlineData(1366, 768, 1.0, false)]
    [InlineData(1920, 1080, 1.5, false)]
    [InlineData(1920, 1080, 2.0, true)]
    [InlineData(1280, 720, 1.25, true)]
    public void 低屏幕自动启用兼容模式(int width, int height, double scale, bool expected)
    {
        var screen = new ScreenMetrics(new PixelPoint((int)(width / scale), (int)(height / scale)), scale, 60);

        Create(new RecordingBackend(), screen).CompatibilityMode.Should().Be(expected);
    }

    /// <summary>用户强制开启时无视屏幕尺寸。</summary>
    [Fact]
    public void 强制兼容模式()
    {
        Create(new RecordingBackend(), FullHd, forceCompatibility: true).CompatibilityMode.Should().BeTrue();
    }

    /// <summary>回车发送一次按下与抬起。</summary>
    [Fact]
    public async Task 回车()
    {
        var backend = new RecordingBackend();

        await Create(backend, FullHd).PressEnterAsync(CancellationToken.None);

        backend.Events.Should().Equal("key(Return,down)", "key(Return,up)");
    }

    /// <summary>剪贴板写入失败时抛异常，而不是静默输入到错误位置。</summary>
    [Fact]
    public async Task 剪贴板失败抛异常()
    {
        var backend = new RecordingBackend { ClipboardSucceeds = false };
        var simulator = Create(backend, FullHd, forceCompatibility: true);

        var action = () => simulator.TypeTextAsync("secret", secret: true, CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>();
    }

    /// <summary>逐字符输入过程中可被取消。</summary>
    [Fact]
    public async Task 输入可取消()
    {
        using var cancellation = new CancellationTokenSource();
        var backend = new RecordingBackend { OnChar = () => cancellation.Cancel() };
        var simulator = Create(backend, FullHd);

        var action = () => simulator.TypeTextAsync("abcd", secret: false, cancellation.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    private static SendInputSimulator Create(
        RecordingBackend backend,
        ScreenMetrics screen,
        bool forceCompatibility = false)
        => new(
            backend,
            screen,
            forceCompatibility,
            NullLogger<SendInputSimulator>.Instance,
            TimeSpan.Zero,
            TimeSpan.Zero);

    private sealed class RecordingBackend : IInputBackend
    {
        public List<string> Events { get; } = [];

        public bool ClipboardSucceeds { get; init; } = true;

        public Action? OnChar { get; init; }

        public void Click(int x, int y) => Events.Add($"click({x},{y})");

        public void SendUnicode(char character)
        {
            Events.Add($"char({character})");
            OnChar?.Invoke();
        }

        public void SendKey(VirtualKey key, bool keyUp)
            => Events.Add($"key({key},{(keyUp ? "up" : "down")})");

        public void SendCtrl(VirtualKey key) => Events.Add($"ctrl({key})");

        public bool TrySetClipboardText(string text)
        {
            Events.Add($"clipboard({text})");
            return ClipboardSucceeds;
        }
    }
}

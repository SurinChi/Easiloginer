using EasiLoginer.Integrations.EasiNote.Environment;
using FluentAssertions;

namespace EasiLoginer.Integrations.Tests;

/// <summary>IWB 判定顺序（文档 §8.2）。</summary>
public sealed class IwbDetectorTests
{
    private const string SeewoDevicePath = @"\\?\hid#vid_1ff7&pid_0f15&mi_00#8&1&0&0000";
    private const string OtherDevicePath = @"\\?\hid#vid_1234&pid_5678&mi_01#8&1&0&0000";

    /// <summary>配置里显式写了 IWB GUID：直接判为白板，且带大括号/小写也要认。</summary>
    [Fact]
    public void 配置覆写为白板()
    {
        var detector = Create(
            new() { [ConfigsFkv.DeviceInfoTypeKey] = "{228756ad-0620-497e-94fb-4022db99c853}" },
            OtherDevicePath);

        detector.Detect().Should().BeTrue();
    }

    /// <summary>配置覆写优先于硬件探测：PC GUID 即使插着希沃触摸也不进白板路径。</summary>
    [Fact]
    public void 配置覆写为普通电脑时忽略硬件()
    {
        var detector = Create(
            new() { [ConfigsFkv.DeviceInfoTypeKey] = "{07BFE486-A99E-430C-8CCE-8A9955417614}" },
            SeewoDevicePath);

        detector.Detect().Should().BeFalse();
    }

    /// <summary>无配置时按 HID 探测。</summary>
    [Fact]
    public void 无配置时按硬件判定()
    {
        Create([], SeewoDevicePath).Detect().Should().BeTrue();
        Create([], OtherDevicePath).Detect().Should().BeFalse();
    }

    /// <summary>HID 不命中时回退到配置缓存 <c>DeviceCache.IsIwb</c>。</summary>
    [Theory]
    [InlineData("true", true)]
    [InlineData("TRUE", true)]
    [InlineData("false", false)]
    [InlineData("", false)]
    public void 缓存兜底(string cached, bool expected)
    {
        var detector = Create(
            new() { [ConfigsFkv.DeviceCacheIsIwbKey] = cached },
            OtherDevicePath);

        detector.Detect().Should().Be(expected);
    }

    /// <summary>启动参数解析：<c>-m</c> / <c>--mode</c>，值转为小写。</summary>
    [Theory]
    [InlineData("-m Display iwb", "display")]
    [InlineData("--mode edit", "edit")]
    [InlineData("-M Display", "display")]
    [InlineData("-m", null)]
    [InlineData("", null)]
    [InlineData("foo bar", null)]
    public void 解析启动模式(string args, string? expected)
    {
        IwbDetector.ParseStartMode(args).Should().Be(expected);
    }

    /// <summary>显式 <c>-m display</c> 无视机器环境；<c>-m edit</c> 同理。</summary>
    [Fact]
    public void 启动参数优先于环境()
    {
        var iwbMachine = Create(
            new() { [ConfigsFkv.DeviceInfoTypeKey] = "{228756AD-0620-497E-94FB-4022DB99C853}" },
            OtherDevicePath);

        iwbMachine.ResolveIsIwb("-m edit").Should().BeFalse();

        var pcMachine = Create(
            new() { [ConfigsFkv.DeviceInfoTypeKey] = "{07BFE486-A99E-430C-8CCE-8A9955417614}" },
            OtherDevicePath);

        pcMachine.ResolveIsIwb("-m Display iwb").Should().BeTrue();
    }

    private static IwbDetector Create(Dictionary<string, string> config, params string[] devicePaths)
        => new(new FakeConfigSource(config), new FakeHidEnumerator(devicePaths));

    private sealed class FakeConfigSource(Dictionary<string, string> values) : IEasiNoteConfigSource
    {
        public string? ReadValue(string name) => values.TryGetValue(name, out var value) ? value : null;
    }

    private sealed class FakeHidEnumerator(string[] paths) : IHidDeviceEnumerator
    {
        public IReadOnlyList<string> EnumerateDevicePaths() => paths;
    }
}

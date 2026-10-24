using EasiLoginer.Integrations.EasiNote.Environment;
using FluentAssertions;

namespace EasiLoginer.Integrations.Tests;

/// <summary>
/// 真机冒烟：HID 枚举的 P/Invoke 必须能在真实环境跑通（不要求插着希沃设备）。
/// </summary>
/// <remarks>
/// 这些用例只验证"不崩溃、结果形态正确"，真正的希沃设备判定仍需要在目标机器上人工确认。
/// </remarks>
public sealed class HardwareSmokeTests
{
    /// <summary>枚举 HID 设备不应抛异常，返回的路径必须是设备接口路径。</summary>
    [Fact]
    public void HID枚举可以运行()
    {
        var enumerator = new WindowsHidDeviceEnumerator();

        var paths = enumerator.EnumerateDevicePaths();

        paths.Should().NotBeNull();
        paths.Where(path => !path.StartsWith(@"\\?\", StringComparison.Ordinal)).Should().BeEmpty();
    }

    /// <summary>用真实配置源与真实枚举器构造判定器，调用不应抛异常。</summary>
    [Fact]
    public void IWB判定可以运行()
    {
        var detector = new IwbDetector(FkvConfigSource.FromEnvironment(), new WindowsHidDeviceEnumerator());

        var isIwb = detector.Detect();

        // 本机没有希沃设备与配置文件时结果应为 false；有设备时为 true 也合法，
        // 因此这里只断言"调用成功"，不锁定具体值。
        _ = isIwb;
    }

    /// <summary>默认 fkv 路径必须落在 <c>%APPDATA%\Seewo\EasiNote5\Data</c> 下。</summary>
    [Fact]
    public void fkv默认路径符合上游约定()
    {
        ConfigsFkv.DefaultPath.Should().EndWith(Path.Combine("Seewo", "EasiNote5", "Data", "Configs.fkv"));
    }
}

using EasiLoginer.Domain.Geometry;
using FluentAssertions;

namespace EasiLoginer.Domain.Tests;

/// <summary>
/// 坐标换算的表驱动测试。
/// </summary>
/// <remarks>
/// 期望值按上游 <c>core/display.py</c> 的逐步截断语义手工推导：
/// 每个中间量都是 <c>int()</c> 截断，不是最后统一取整。
/// 这些用例是"与 Python 逐点相等"的验收依据（文档 §13.2）。
/// </remarks>
public sealed class LoginWindowGeometryTests
{
    private static readonly PixelPoint BaseSize = new(1920, 1080);
    private static readonly PixelPoint LoginWindowSize = new(808, 582);

    /// <summary>缩放 100% 时，窗口内坐标应保持不变（窗口居中且基准分辨率等于物理分辨率）。</summary>
    [Theory]
    [InlineData(1090, 350)]
    [InlineData(1000, 420)]
    [InlineData(1000, 490)]
    [InlineData(935, 724)]
    [InlineData(1090, 560)]
    public void 缩放100时窗口内坐标保持不变(int x, int y)
    {
        var result = LoginWindowGeometry.ToScreen(
            new PixelPoint(x, y), LoginWindowSize, BaseSize, new PixelPoint(1920, 1080), 1.0);

        result.Should().Be(new PixelPoint(x, y));
    }

    /// <summary>125% 下按上游算法逐步截断的期望值。</summary>
    [Theory]
    [InlineData(1090, 350, 1362, 437)]
    [InlineData(1000, 420, 1250, 524)]
    [InlineData(935, 724, 1168, 904)]
    public void 缩放125时与上游算法一致(int x, int y, int expectedX, int expectedY)
    {
        var result = LoginWindowGeometry.ToScreen(
            new PixelPoint(x, y), LoginWindowSize, BaseSize, new PixelPoint(2400, 1350), 1.25);

        result.Should().Be(new PixelPoint(expectedX, expectedY));
    }

    /// <summary>200%（4K）下的期望值。</summary>
    [Fact]
    public void 缩放200时与上游算法一致()
    {
        var result = LoginWindowGeometry.ToScreen(
            new PixelPoint(1090, 350), LoginWindowSize, BaseSize, new PixelPoint(3840, 2160), 2.0);

        result.Should().Be(new PixelPoint(2180, 700));
    }

    /// <summary>「进入登录界面」按钮走独立公式，不参与窗口相对换算。</summary>
    [Theory]
    [InlineData(1.0, 172, 1044)]
    [InlineData(1.25, 215, 1305)]
    [InlineData(2.0, 344, 2088)]
    public void 进入登录界面的换算与上游一致(double scale, int expectedX, int expectedY)
    {
        var physical = scale switch
        {
            1.0 => new PixelPoint(1920, 1080),
            1.25 => new PixelPoint(2400, 1350),
            _ => new PixelPoint(3840, 2160),
        };

        var result = LoginWindowGeometry.ToScreenEnterLogin(
            new PixelPoint(172, 1044), BaseSize, physical, scale);

        result.Should().Be(new PixelPoint(expectedX, expectedY));
    }

    /// <summary>物理尺寸必须按逻辑尺寸乘缩放后截断，与 <c>get_screen_size_physical()</c> 一致。</summary>
    [Fact]
    public void 物理尺寸按缩放截断()
    {
        var metrics = new ScreenMetrics(new PixelPoint(1920, 1080), 1.25, 60);

        metrics.PhysicalSize.Should().Be(new PixelPoint(2400, 1350));
    }

    /// <summary>刷新率被限制在 [30, 240]，帧间隔下限 1 ms。</summary>
    [Theory]
    [InlineData(10, 30, 33)]
    [InlineData(60, 60, 16)]
    [InlineData(144, 144, 6)]
    [InlineData(500, 240, 4)]
    public void 刷新率被约束后换算帧间隔(int raw, int normalized, int interval)
    {
        var metrics = new ScreenMetrics(new PixelPoint(1920, 1080), 1.0, raw);

        metrics.NormalizedRefreshRate.Should().Be(normalized);
        metrics.AnimationFrameIntervalMilliseconds.Should().Be(interval);
    }
}

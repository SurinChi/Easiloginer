using System.Windows.Media;
using EasiLoginer.Domain.Configuration;
using EasiLoginer.Presentation.Overlays;
using FluentAssertions;

namespace EasiLoginer.UiTests;

/// <summary>警示横幅的配置映射与滚动逻辑（文档 §10.5）。</summary>
public sealed class BannerTests
{
    /// <summary>配置映射：颜色、范围钳制与 DIP 换算。</summary>
    [Fact]
    public void 配置映射()
    {
        var config = new BannerConfig();
        config.Style.Fps = 500;
        config.Style.TextSpeed = 99;
        config.Style.YOffset = 30;

        var options = BannerOptions.FromConfig(config, scale: 1.5);

        options.Text.Should().Be(config.Style.Text);
        options.FontFamily.Should().Be("HarmonyOS Sans SC");
        options.Fps.Should().Be(120, "FPS 上限是 120");
        options.Speed.Should().Be(8, "滚动距离上限是 8");
        options.Top.Should().BeApproximately(20, 1e-9, "YOffset 按缩放折算成 DIP");
        options.Height.Should().BeApproximately(140 / 1.5, 1e-9);
    }

    /// <summary>颜色按 ARGB 通道还原（<c>#B4E4080A</c>）。</summary>
    [Fact]
    public void 颜色转换()
    {
        var color = BannerOptions.ToColor(ArgbColor.Parse("#B4E4080A"));

        color.A.Should().Be(0xB4);
        color.R.Should().Be(0xE4);
        color.G.Should().Be(0x08);
        color.B.Should().Be(0x0A);
    }

    /// <summary>帧间隔按 FPS 换算，且至少 1 ms。</summary>
    [Theory]
    [InlineData(60, 16)]
    [InlineData(120, 8)]
    [InlineData(1, 1000)]
    public void 帧间隔(int fps, int expected)
    {
        var options = BannerOptions.FromConfig(new BannerConfig { Style = new BannerStyleConfig { Fps = fps } }, 1.0);

        options.FrameIntervalMilliseconds.Should().Be(expected);
    }

    /// <summary>正速度向左滚：每帧位移等于配置的速度。</summary>
    [Fact]
    public void 正向滚动()
    {
        var scroll = new BannerScroll();
        scroll.Reset(100);

        scroll.Advance(3, textWidth: 200, viewportWidth: 800);

        scroll.Offset.Should().Be(97);
    }

    /// <summary>滚出左侧后回绕到右侧视口。</summary>
    [Fact]
    public void 正向回绕()
    {
        var scroll = new BannerScroll();
        scroll.Reset(-199);

        scroll.Advance(3, textWidth: 200, viewportWidth: 800);

        scroll.Offset.Should().Be(800);
    }

    /// <summary>负速度向右滚，越过右边界后回到左侧。</summary>
    [Fact]
    public void 反向滚动与回绕()
    {
        var scroll = new BannerScroll();
        scroll.Reset(790);

        scroll.Advance(-5, textWidth: 200, viewportWidth: 800);
        scroll.Offset.Should().Be(795);

        scroll.Advance(-5, textWidth: 200, viewportWidth: 800);
        scroll.Offset.Should().Be(-200, "越过视口右边界后回到文本左侧");
    }

    /// <summary>速度为零时位置不动。</summary>
    [Fact]
    public void 零速度不动()
    {
        var scroll = new BannerScroll();
        scroll.Reset(123);

        scroll.Advance(0, textWidth: 200, viewportWidth: 800);

        scroll.Offset.Should().Be(123);
    }
}

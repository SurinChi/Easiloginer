using EasiLoginer.Presentation.Login;
using FluentAssertions;

namespace EasiLoginer.UiTests;

/// <summary>隐私遮罩的物理像素 → DIP 换算（文档 §10.5）。</summary>
public sealed class PrivacyMaskGeometryTests
{
    /// <summary>100% 缩放下 DIP 等于物理像素。</summary>
    [Fact]
    public void 一百缩放保持不变()
    {
        var rect = PrivacyMaskGeometry.ToDips(868, 381, 440, 386, 1.0);

        rect.Should().Be((868d, 381d, 440d, 386d));
    }

    /// <summary>150% / 200% 下按缩放折算。</summary>
    [Theory]
    [InlineData(1.5, 578.6666666666666, 254, 293.3333333333333, 257.3333333333333)]
    [InlineData(2.0, 434, 190.5, 220, 193)]
    public void 高缩放折算(double scale, double left, double top, double width, double height)
    {
        var rect = PrivacyMaskGeometry.ToDips(868, 381, 440, 386, scale);

        rect.Left.Should().BeApproximately(left, 1e-6);
        rect.Top.Should().BeApproximately(top, 1e-6);
        rect.Width.Should().BeApproximately(width, 1e-6);
        rect.Height.Should().BeApproximately(height, 1e-6);
    }

    /// <summary>缩放非法时按 1.0 兜底，避免出现 NaN 尺寸。</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    public void 非法缩放按一兜底(double scale)
    {
        var rect = PrivacyMaskGeometry.ToDips(100, 200, 300, 400, scale);

        rect.Should().Be((100d, 200d, 300d, 400d));
    }

    /// <summary>尺寸不会小于 1 DIP（避免 0 宽度窗口抛异常）。</summary>
    [Fact]
    public void 最小尺寸兜底()
    {
        var rect = PrivacyMaskGeometry.ToDips(0, 0, 1, 1, 4.0);

        rect.Width.Should().Be(1);
        rect.Height.Should().Be(1);
    }
}

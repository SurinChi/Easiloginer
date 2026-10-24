using EasiLoginer.Domain.Geometry;
using FluentAssertions;

namespace EasiLoginer.Domain.Tests;

/// <summary>
/// <see cref="PixelPoint"/> 的取整语义。
/// </summary>
/// <remarks>
/// 上游每次运算都用 <c>Point()</c> 把结果转成 <c>int</c>，Python 的 <c>int()</c>
/// 向零截断（不是四舍五入、也不是向下取整）。负数同样向零截断：
/// <c>int(-3.5) == -3</c>。
/// </remarks>
public sealed class PixelPointTests
{
    /// <summary>除法等价于乘以倒数，且结果向零截断。</summary>
    [Theory]
    [InlineData(5, 2, 2)]
    [InlineData(1, 2, 0)]
    [InlineData(7, 2, 3)]
    [InlineData(-7, 2, -3)]
    [InlineData(-1, 2, 0)]
    public void 除法向零截断(int x, int divisor, int expected)
    {
        var result = new PixelPoint(x, x) / divisor;

        result.X.Should().Be(expected);
        result.Y.Should().Be(expected);
    }

    /// <summary>缩放同样向零截断，而不是四舍五入。</summary>
    [Theory]
    [InlineData(1090, 1.25, 1362)]
    [InlineData(935, 1.25, 1168)]
    [InlineData(727, 1.5, 1090)]
    public void 缩放向零截断(int x, double scale, int expected)
    {
        (new PixelPoint(x, 0) * scale).X.Should().Be(expected);
    }

    /// <summary>坐标为整数，加减不产生小数。</summary>
    [Fact]
    public void 加减保持整数()
    {
        var a = new PixelPoint(1920, 1080);
        var b = new PixelPoint(808, 582);

        (a - b).Should().Be(new PixelPoint(1112, 498));
        (a + b).Should().Be(new PixelPoint(2728, 1662));
    }

    /// <summary>除数为零时抛异常，而不是产生无穷大再截断成垃圾值。</summary>
    [Fact]
    public void 除数为零时抛异常()
    {
        var action = () => _ = new PixelPoint(1, 1) / 0;

        action.Should().Throw<DivideByZeroException>();
    }
}

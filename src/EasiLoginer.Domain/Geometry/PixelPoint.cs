namespace EasiLoginer.Domain.Geometry;

/// <summary>
/// 屏幕坐标点（整数像素），对应上游 <c>core/display.py::Point</c>。
/// </summary>
/// <remarks>
/// <para>
/// 与上游一致：<b>每一步运算都立即截断为整数</b>（向零取整），而不是最后统一取整。
/// 因此 <c>(a - b) / 2</c> 等价于 <c>(int)((a - b) * 0.5)</c>。
/// 这一点直接决定登录点击是否落在按钮上，不要"顺手"改成浮点累加。
/// </para>
/// <para>
/// 上游的 <c>Point.scale</c> 是类级全局缓存（首次访问后固定），属于隐藏状态；
/// 本项目不复刻，缩放比例一律由调用方显式传入，并在每次登录开始时解析一次
/// （文档 §5.3）。
/// </para>
/// </remarks>
public readonly record struct PixelPoint(int X, int Y)
{
    /// <summary>坐标相加。</summary>
    public static PixelPoint operator +(PixelPoint left, PixelPoint right)
        => new(left.X + right.X, left.Y + right.Y);

    /// <summary>坐标相减。</summary>
    public static PixelPoint operator -(PixelPoint left, PixelPoint right)
        => new(left.X - right.X, left.Y - right.Y);

    /// <summary>按比例缩放，结果向零截断（与 Python <c>int()</c> 一致）。</summary>
    public static PixelPoint operator *(PixelPoint point, double factor)
        => new((int)(point.X * factor), (int)(point.Y * factor));

    /// <summary>
    /// 除以一个数。实现为乘以 <c>1 / divisor</c>，与上游 <c>Point.__truediv__</c> 逐位一致。
    /// </summary>
    public static PixelPoint operator /(PixelPoint point, double divisor)
    {
        if (divisor == 0)
        {
            throw new DivideByZeroException("坐标换算的除数不能为零。");
        }

        return point * (1 / divisor);
    }

    /// <summary>以 <c>(x, y)</c> 形式输出，便于日志与断言。</summary>
    public override string ToString() => $"({X}, {Y})";
}

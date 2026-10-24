namespace EasiLoginer.Domain.Geometry;

/// <summary>
/// 登录窗口内的坐标换算，对应上游 <c>calc_relative_login_window_position()</c>。
/// </summary>
/// <remarks>
/// 复刻公式（文档 §5.3，与 Python 实现逐字对应）：
/// <code>
/// windowPosition  = (baseSize - loginWindowSize) / 2
/// relative        = position - windowPosition
/// scaledRelative  = relative * scale
/// scaledTopLeft   = (physicalScreen - loginWindowSize * scale) / 2
/// result          = scaledRelative + scaledTopLeft
/// </code>
/// 每一步都按 <see cref="PixelPoint"/> 的规则截断为整数。
/// </remarks>
public static class LoginWindowGeometry
{
    /// <summary>
    /// 把配置中的登录窗口内坐标换算为当前屏幕的物理像素坐标。
    /// </summary>
    /// <param name="position">配置坐标（基于 <paramref name="baseSize"/>）。</param>
    /// <param name="loginWindowSize">登录窗口尺寸（配置值，未缩放）。</param>
    /// <param name="baseSize">配置坐标所基于的基准分辨率。</param>
    /// <param name="physicalScreenSize">当前主屏物理像素尺寸。</param>
    /// <param name="scale">当前主屏缩放比例。</param>
    public static PixelPoint ToScreen(
        PixelPoint position,
        PixelPoint loginWindowSize,
        PixelPoint baseSize,
        PixelPoint physicalScreenSize,
        double scale)
    {
        var windowPosition = (baseSize - loginWindowSize) * 0.5;
        var relative = position - windowPosition;
        var scaledRelative = relative * scale;
        var scaledTopLeft = (physicalScreenSize - (loginWindowSize * scale)) * 0.5;
        return scaledRelative + scaledTopLeft;
    }

    /// <summary>
    /// 「进入登录界面」按钮的坐标换算——<b>唯一</b>不走窗口相对换算的特例。
    /// </summary>
    /// <remarks>
    /// 上游 <c>FixedAutomator.login()</c> 直接算：
    /// <c>x = x * scale</c>、<c>y = screenHeight - (baseHeight - y) * scale</c>，
    /// 结果作为浮点交给 pyautogui，最终由 Win32 边界截断为整数。
    /// 因此这里同样先算浮点、再对最终值截断（不能拆成两次截断）。
    /// </remarks>
    public static PixelPoint ToScreenEnterLogin(
        PixelPoint position,
        PixelPoint baseSize,
        PixelPoint physicalScreenSize,
        double scale)
    {
        var x = position.X * scale;
        var y = physicalScreenSize.Y - ((baseSize.Y - position.Y) * scale);
        return new PixelPoint((int)x, (int)y);
    }
}

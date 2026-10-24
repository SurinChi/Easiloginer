namespace EasiLoginer.Presentation.Login;

/// <summary>
/// 隐私遮罩的坐标换算：登录策略给出的是<b>物理像素</b>，WPF 窗口使用 DIP。
/// </summary>
/// <remarks>
/// 单列成纯函数便于测试，避免依赖截图或真实窗口才能验证（文档 §10.5）。
/// </remarks>
public static class PrivacyMaskGeometry
{
    /// <summary>把物理像素矩形换算为 DIP；缩放非法时按 1.0 处理。</summary>
    public static (double Left, double Top, double Width, double Height) ToDips(
        int x,
        int y,
        int width,
        int height,
        double scale)
    {
        // NaN / 0 / 负数都回退到 1.0，避免算出 NaN 尺寸
        var effective = scale > 0 ? scale : 1.0;

        return (
            x / effective,
            y / effective,
            Math.Max(1, width / effective),
            Math.Max(1, height / effective));
    }
}

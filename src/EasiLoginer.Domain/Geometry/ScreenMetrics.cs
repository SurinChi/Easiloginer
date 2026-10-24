namespace EasiLoginer.Domain.Geometry;

/// <summary>
/// 一次登录开始时解析出的屏幕度量快照。
/// </summary>
/// <remarks>
/// 上游用全局 <c>Point.scale</c> 缓存缩放比例，导致显示设置变化后行为漂移；
/// 本项目改为每次登录解析一次快照并显式传递（文档 §5.3）。
/// 物理尺寸的算法与 <c>get_screen_size_physical()</c> 一致：
/// 逻辑尺寸乘以缩放后各自截断。
/// </remarks>
/// <param name="LogicalSize">主屏逻辑尺寸（DIP）。</param>
/// <param name="Scale">主屏缩放比例（如 1.0 / 1.25 / 2.0）。</param>
/// <param name="RefreshRateHz">主屏刷新率（Hz），未取到时回退 60。</param>
public sealed record ScreenMetrics(PixelPoint LogicalSize, double Scale, int RefreshRateHz)
{
    /// <summary>主屏物理像素尺寸，对应 <c>get_screen_size_physical()</c>。</summary>
    public PixelPoint PhysicalSize
        => new((int)(LogicalSize.X * Scale), (int)(LogicalSize.Y * Scale));

    /// <summary>刷新率限制在 [30, 240]，对应 <c>get_main_display_refresh_rate()</c>。</summary>
    public int NormalizedRefreshRate => Math.Clamp(RefreshRateHz, 30, 240);

    /// <summary>动画帧间隔（毫秒），对应 <c>get_animation_frame_interval()</c>，下限 1 ms。</summary>
    public int AnimationFrameIntervalMilliseconds
        => Math.Max(1, (int)(1000.0 / NormalizedRefreshRate));
}

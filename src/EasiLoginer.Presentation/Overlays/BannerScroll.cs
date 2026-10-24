namespace EasiLoginer.Presentation.Overlays;

/// <summary>
/// 横幅文字滚动状态：每帧按配置速度位移，越界后从另一侧回绕。
/// </summary>
/// <remarks>
/// 抽成纯逻辑便于单测（文档 §10.5 的"文字每次刷新滚动的距离"）。
/// 速度为负表示反向滚动。
/// </remarks>
public sealed class BannerScroll
{
    /// <summary>当前横坐标（DIP）。</summary>
    public double Offset { get; private set; }

    /// <summary>把位置重置到起始处（通常为视口宽度）。</summary>
    public void Reset(double offset) => Offset = offset;

    /// <summary>推进一帧。</summary>
    /// <param name="speed">每帧位移；正数向左滚。</param>
    /// <param name="textWidth">文本宽度。</param>
    /// <param name="viewportWidth">视口宽度。</param>
    public void Advance(double speed, double textWidth, double viewportWidth)
    {
        Offset -= speed;

        if (speed >= 0)
        {
            if (Offset <= -textWidth)
            {
                Offset = viewportWidth;
            }
        }
        else if (Offset >= viewportWidth)
        {
            Offset = -textWidth;
        }
    }
}

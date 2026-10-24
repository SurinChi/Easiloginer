using System.Windows.Media;
using EasiLoginer.Domain.Configuration;

namespace EasiLoginer.Presentation.Overlays;

/// <summary>警示横幅的呈现参数（由配置换算而来）。</summary>
/// <param name="Text">滚动文本。</param>
/// <param name="FontFamily">字体名。</param>
/// <param name="TextColor">文字颜色。</param>
/// <param name="ForegroundColor">条纹前景色。</param>
/// <param name="BackgroundColor">背景色。</param>
/// <param name="Fps">刷新率（1–120）。</param>
/// <param name="Speed">每帧滚动距离（-8–8）。</param>
/// <param name="Top">距屏幕顶部的 DIP 偏移。</param>
/// <param name="Height">横幅高度（DIP）。</param>
public sealed record BannerOptions(
    string Text,
    string FontFamily,
    Color TextColor,
    Color ForegroundColor,
    Color BackgroundColor,
    int Fps,
    int Speed,
    double Top,
    double Height)
{
    /// <summary>从配置构造；物理像素按缩放换算成 DIP。</summary>
    public static BannerOptions FromConfig(BannerConfig config, double scale)
    {
        ArgumentNullException.ThrowIfNull(config);

        var style = config.Style;
        var effectiveScale = scale > 0 ? scale : 1.0;

        return new BannerOptions(
            Text: style.Text,
            FontFamily: style.TextFont,
            TextColor: ToColor(style.TextColor),
            ForegroundColor: ToColor(style.FgColor),
            BackgroundColor: ToColor(style.BgColor),
            Fps: Math.Clamp(style.Fps, 1, 120),
            Speed: Math.Clamp(style.TextSpeed, -8, 8),
            Top: style.YOffset / effectiveScale,
            Height: 140 / effectiveScale);
    }

    /// <summary>每帧间隔（毫秒），对应文档 §5.1 的 Fps。</summary>
    public int FrameIntervalMilliseconds => Math.Max(1, (int)(1000.0 / Math.Clamp(Fps, 1, 120)));

    /// <summary>把 <c>#AARRGGBB</c> 值对象转换为 WPF 颜色。</summary>
    public static Color ToColor(ArgbColor color) => Color.FromArgb(color.A, color.R, color.G, color.B);
}

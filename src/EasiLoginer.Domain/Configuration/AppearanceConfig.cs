using System.Text.Json.Serialization;

namespace EasiLoginer.Domain.Configuration;

/// <summary>
/// 外观与背景设置：亚克力（毛玻璃）效果与窗口背景来源。
/// </summary>
/// <remarks>
/// <para>
/// 亚克力由应用内对背景层做模糊实现，不依赖 Windows 合成器的系统亚克力，
/// 因此「模糊强度」可以随配置调整，并且在 Windows 10 / 11 上表现一致。
/// </para>
/// <para>
/// 背景图片 / 壁纸模式下，<see cref="AcrylicOpacity"/> 控制覆盖在模糊背景上的
/// 主题色玻璃层的不透明度；纯色、渐变和跟随主题模式不使用该值（没有可透视的
/// 背景图）。这是 EasiLoginer 自有扩展段，上游会忽略。
/// </para>
/// </remarks>
public sealed class AppearanceConfig
{
    /// <summary>启用亚克力（毛玻璃）效果。</summary>
    /// <remarks>关闭后回到跟随主题的纯色背景，背景模式设置不再生效。</remarks>
    [JsonPropertyName("AcrylicEnabled")]
    public bool AcrylicEnabled { get; set; } = true;

    /// <summary>背景模糊强度（0–60），数值越大越模糊。</summary>
    [JsonPropertyName("BlurStrength")]
    public int BlurStrength { get; set; } = 32;

    /// <summary>
    /// 亚克力玻璃层的不透明度（20–100，单位百分比）。
    /// </summary>
    /// <remarks>100 表示完全不透明，0 附近会严重影响文字可读性，因此下限为 20。</remarks>
    [JsonPropertyName("AcrylicOpacity")]
    public int AcrylicOpacity { get; set; } = 78;

    /// <summary>背景来源。</summary>
    [JsonPropertyName("BackgroundMode")]
    public BackgroundMode BackgroundMode { get; set; } = BackgroundMode.Wallpaper;

    /// <summary>纯色背景颜色；渐变模式下作为起始颜色。</summary>
    [JsonPropertyName("BackgroundColor")]
    public ArgbColor BackgroundColor { get; set; } = ArgbColor.Parse("#FF1E1E22");

    /// <summary>渐变模式的结束颜色。</summary>
    [JsonPropertyName("BackgroundColor2")]
    public ArgbColor BackgroundColor2 { get; set; } = ArgbColor.Parse("#FF34343C");

    /// <summary>本地背景图片路径（仅 <see cref="BackgroundMode.Image"/> 使用）。</summary>
    [JsonPropertyName("BackgroundImagePath")]
    public string BackgroundImagePath { get; set; } = string.Empty;

    /// <summary>模糊强度是否生效（仅壁纸 / 图片背景有意义）。</summary>
    [JsonIgnore]
    public bool HasBlurrableBackground
        => BackgroundMode is BackgroundMode.Wallpaper or BackgroundMode.Image;

    /// <summary>玻璃层的实际不透明度（0–1），已按配置下限钳制。</summary>
    [JsonIgnore]
    public double GlassOpacity => Math.Clamp(AcrylicOpacity / 100.0, 0.2, 1.0);

    /// <summary>实际模糊半径；不可模糊的背景返回 0。</summary>
    [JsonIgnore]
    public double EffectiveBlurRadius => HasBlurrableBackground ? Math.Clamp(BlurStrength, 0, 60) : 0;
}

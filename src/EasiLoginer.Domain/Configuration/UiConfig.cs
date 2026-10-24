using System.Text.Json.Serialization;
using EasiLoginer.Domain.Geometry;

namespace EasiLoginer.Domain.Configuration;

/// <summary>运行前警告弹窗，对应上游 <c>WarningConfig</c>。</summary>
public sealed class WarningConfig
{
    /// <summary>启用警告弹窗。</summary>
    [JsonPropertyName("Enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>等待确认的最大时长（秒），范围 5–600。</summary>
    [JsonPropertyName("Timeout")]
    public int Timeout { get; set; } = 60;

    /// <summary>最大推迟次数，范围 0–3。</summary>
    [JsonPropertyName("MaxDelays")]
    public int MaxDelays { get; set; } = 1;

    /// <summary>
    /// 每次推迟后的等待时长（秒），范围 5–300。
    /// 旧配置中的键名是 <c>Delay</c>，读取时由迁移层改名。
    /// </summary>
    [JsonPropertyName("DelayTime")]
    public int DelayTime { get; set; } = 150;

    /// <summary>在弹窗中显示用户名。</summary>
    [JsonPropertyName("ShowUserName")]
    public bool ShowUserName { get; set; } = true;
}

/// <summary>警示横幅的样式，对应上游 <c>BannerStyleConfig</c>。</summary>
public sealed class BannerStyleConfig
{
    /// <summary>滚动文本（保留上游原文，含前后空格与 emoji）。</summary>
    [JsonPropertyName("Text")]
    public string Text { get; set; } = "  ⚠️WARNING⚠️  正在运行希沃白板自动登录  请勿触摸一体机";

    /// <summary>字体名。</summary>
    [JsonPropertyName("TextFont")]
    public string TextFont { get; set; } = "HarmonyOS Sans SC";

    /// <summary>文字颜色。</summary>
    [JsonPropertyName("TextColor")]
    public ArgbColor TextColor { get; set; } = ArgbColor.Parse("#FFFFDE59");

    /// <summary>前景色。</summary>
    [JsonPropertyName("FgColor")]
    public ArgbColor FgColor { get; set; } = ArgbColor.Parse("#C8FFDE59");

    /// <summary>背景色。</summary>
    [JsonPropertyName("BgColor")]
    public ArgbColor BgColor { get; set; } = ArgbColor.Parse("#B4E4080A");

    /// <summary>每秒刷新次数，范围 1–120。</summary>
    [JsonPropertyName("Fps")]
    public int Fps { get; set; } = 60;

    /// <summary>每次刷新滚动的距离，范围 -8–8。</summary>
    [JsonPropertyName("TextSpeed")]
    public int TextSpeed { get; set; } = 3;

    /// <summary>横幅距屏幕顶部的像素偏移。</summary>
    [JsonPropertyName("YOffset")]
    public int YOffset { get; set; } = 20;
}

/// <summary>警示横幅，对应上游 <c>BannerConfig</c>。</summary>
public sealed class BannerConfig
{
    /// <summary>启用警示横幅。</summary>
    [JsonPropertyName("Enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>横幅样式。</summary>
    [JsonPropertyName("Style")]
    public BannerStyleConfig Style { get; set; } = new();
}

/// <summary>状态浮窗，对应上游 <c>StatusOverlayConfig</c>。</summary>
public sealed class StatusOverlayConfig
{
    /// <summary>启用状态浮窗。</summary>
    [JsonPropertyName("Enabled")]
    public bool Enabled { get; set; } = true;
}

/// <summary>隐私保护遮罩，对应上游 <c>PrivacyMaskConfig</c>。</summary>
public sealed class PrivacyMaskConfig
{
    /// <summary>启用遮罩。</summary>
    [JsonPropertyName("Enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>遮罩左上角物理坐标。</summary>
    [JsonPropertyName("MaskLeftTop")]
    public PixelPoint MaskLeftTop { get; set; } = new(868, 381);

    /// <summary>遮罩大小。</summary>
    [JsonPropertyName("MaskSize")]
    public PixelPoint MaskSize { get; set; } = new(440, 386);
}

/// <summary>实验性选项，对应上游 <c>ExperimentalConfig</c>。</summary>
public sealed class ExperimentalConfig
{
    /// <summary>隐私保护遮罩。</summary>
    [JsonPropertyName("PrivacyMask")]
    public PrivacyMaskConfig PrivacyMask { get; set; } = new();
}

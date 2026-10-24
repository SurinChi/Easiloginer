using System.Text.Json.Serialization;

namespace EasiLoginer.Domain.Configuration;

/// <summary>
/// 应用配置根对象，对应上游 <c>Config</c>。
/// </summary>
/// <remarks>
/// <para>与上游的差异（均为文档明确要求）：</para>
/// <list type="bullet">
///   <item>移除 <c>ClassIsland</c> 整段（§2.5），旧配置读取时丢弃；</item>
///   <item><c>App.TelemetryEnabled</c> 默认 false（§5.1）；</item>
///   <item><c>Update.Mode</c> 默认 0（§5.1 / §11.3）；</item>
///   <item><c>Internal.IsAutomationPageNoticeShown</c> 移除（§5.1）。</item>
/// </list>
/// <para>字段名即 JSON 键名，且全部显式标注，不依赖任何命名策略（§4.4）。</para>
/// </remarks>
public sealed class Config
{
    /// <summary>登录选项。</summary>
    [JsonPropertyName("Login")]
    public LoginConfig Login { get; set; } = new();

    /// <summary>警告弹窗。</summary>
    [JsonPropertyName("Warning")]
    public WarningConfig Warning { get; set; } = new();

    /// <summary>警示横幅。</summary>
    [JsonPropertyName("Banner")]
    public BannerConfig Banner { get; set; } = new();

    /// <summary>状态浮窗。</summary>
    [JsonPropertyName("StatusOverlay")]
    public StatusOverlayConfig StatusOverlay { get; set; } = new();

    /// <summary>应用设置。</summary>
    [JsonPropertyName("App")]
    public AppConfig App { get; set; } = new();

    /// <summary>实验性选项。</summary>
    [JsonPropertyName("Experimental")]
    public ExperimentalConfig Experimental { get; set; } = new();

    /// <summary>档案设置。</summary>
    [JsonPropertyName("Profile")]
    public ProfileConfig Profile { get; set; } = new();

    /// <summary>更新设置。</summary>
    [JsonPropertyName("Update")]
    public UpdateConfig Update { get; set; } = new();

    /// <summary>调试选项。</summary>
    [JsonPropertyName("Debug")]
    public DebugConfig Debug { get; set; } = new();

    /// <summary>内部数据。</summary>
    [JsonPropertyName("Internal")]
    public InternalConfig Internal { get; set; } = new();

    /// <summary>统计数据。</summary>
    [JsonPropertyName("Statistics")]
    public StatisticsConfig Statistics { get; set; } = new();
}

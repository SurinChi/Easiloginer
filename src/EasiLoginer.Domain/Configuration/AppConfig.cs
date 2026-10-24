using System.Text.Json.Serialization;

namespace EasiLoginer.Domain.Configuration;

/// <summary>应用设置，对应上游 <c>AppConfig</c>。</summary>
public sealed class AppConfig
{
    /// <summary>最大重试次数，范围 0–5。</summary>
    [JsonPropertyName("MaxRetries")]
    public int MaxRetries { get; set; } = 2;

    /// <summary>应用主题。</summary>
    [JsonPropertyName("Theme")]
    public ThemeOptions Theme { get; set; } = ThemeOptions.Auto;

    /// <summary>启用日志记录。</summary>
    [JsonPropertyName("LogEnabled")]
    public bool LogEnabled { get; set; } = true;

    /// <summary>
    /// 参与匿名使用情况分析。
    /// 上游默认 true；EasiLoginer 默认 <b>false</b>，且只有配置了自己的 Sentry DSN 才会真正上报
    /// （文档 §0.2 / §5.1）。
    /// </summary>
    [JsonPropertyName("TelemetryEnabled")]
    public bool TelemetryEnabled { get; set; }
}

/// <summary>档案设置，对应上游 <c>ProfileConfig</c>。</summary>
public sealed class ProfileConfig
{
    /// <summary>保存档案时跳过账号校验。</summary>
    [JsonPropertyName("SkipVerify")]
    public bool SkipVerify { get; set; }
}

/// <summary>更新设置，对应上游 <c>UpdateConfig</c>。</summary>
public sealed class UpdateConfig
{
    /// <summary>
    /// 更新模式。上游默认 3（自动安装）；EasiLoginer 首发默认 0（从不自动更新），
    /// 待自有清单可用后再允许用户开启（文档 §5.1 / §11.3）。
    /// </summary>
    [JsonPropertyName("Mode")]
    public UpdateMode Mode { get; set; } = UpdateMode.Never;

    /// <summary>登录完成后检查更新。</summary>
    [JsonPropertyName("CheckAfterLogin")]
    public bool CheckAfterLogin { get; set; } = true;

    /// <summary>目标更新通道。</summary>
    [JsonPropertyName("TargetUpdateChannel")]
    public UpdateChannel TargetUpdateChannel { get; set; } = UpdateChannel.Release;

    /// <summary>下载镜像源。</summary>
    [JsonPropertyName("TargetDownloadSource")]
    public DownloadSource TargetDownloadSource { get; set; } = DownloadSource.Auto;

    /// <summary>上个版本（隐藏项，用于更新成功后提示）。</summary>
    [JsonPropertyName("LastVersion")]
    public string LastVersion { get; set; } = "Unknown";
}

/// <summary>调试选项，对应上游 <c>DebugConfig</c>。</summary>
public sealed class DebugConfig
{
    /// <summary>启用彩蛋。</summary>
    [JsonPropertyName("EasterEggEnabled")]
    public bool EasterEggEnabled { get; set; }

    /// <summary>启用开发选项。</summary>
    [JsonPropertyName("DebugMode")]
    public bool DebugMode { get; set; }

    /// <summary>启用诊断日志。</summary>
    [JsonPropertyName("VerboseLog")]
    public bool VerboseLog { get; set; }

    /// <summary>改用包含匹配查找窗口。</summary>
    [JsonPropertyName("AlternateFindWindowMethod")]
    public bool AlternateFindWindowMethod { get; set; }
}

/// <summary>内部数据，对应上游 <c>InternalConfig</c>。</summary>
/// <remarks>
/// 上游的 <c>IsAutomationPageNoticeShown</c> 随 ClassIsland 一起移除，
/// 旧配置读取时丢弃（文档 §5.1 / §2.5）。
/// </remarks>
public sealed class InternalConfig
{
    /// <summary>首次向导已完成。</summary>
    [JsonPropertyName("IsOobeCompleted")]
    public bool IsOobeCompleted { get; set; }

    /// <summary>档案页提示已展示。</summary>
    [JsonPropertyName("IsProfilePageNoticeShown")]
    public bool IsProfilePageNoticeShown { get; set; }

    /// <summary>上次检查更新的时间。</summary>
    [JsonPropertyName("LastUpdateCheckTime")]
    public DateTimeOffset? LastUpdateCheckTime { get; set; }

    /// <summary>已忽略的公告 ID。</summary>
    [JsonPropertyName("HiddenAnnouncementIds")]
    public List<string> HiddenAnnouncementIds { get; set; } = [];
}

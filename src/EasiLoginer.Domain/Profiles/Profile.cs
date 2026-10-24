using System.Text.Json.Serialization;

namespace EasiLoginer.Domain.Profiles;

/// <summary>
/// 档案根对象，对应上游 <c>Profile</c>。
/// </summary>
/// <remarks>
/// 上游的 <c>notifier</c>（QObject）不移植：变更通知改由应用层事件承担
/// （文档 §1.2 / §5.2）。
/// </remarks>
public sealed class Profile
{
    /// <summary>档案格式版本。</summary>
    [JsonPropertyName("schema_version")]
    public int SchemaVersion { get; set; } = ProfileSchema.Version;

    /// <summary>是否启用主密码加密。</summary>
    [JsonPropertyName("encryption_enabled")]
    public bool EncryptionEnabled { get; set; }

    /// <summary>主密码盐值（Base64，16 字节）。</summary>
    [JsonPropertyName("encryption_salt")]
    public string? EncryptionSalt { get; set; }

    /// <summary>主密码校验密文（<c>ea$</c> 前缀）。</summary>
    [JsonPropertyName("encryption_check")]
    public string? EncryptionCheck { get; set; }

    /// <summary>允许在本机免密查看档案密码。</summary>
    [JsonPropertyName("passwordless_view")]
    public bool PasswordlessView { get; set; } = true;

    /// <summary>档案列表。</summary>
    [JsonPropertyName("automations")]
    public List<AutomationProfile> Automations { get; set; } = [];
}

using System.Text.Json;
using System.Text.Json.Serialization;

namespace EasiLoginer.Domain.Profiles;

/// <summary>
/// 一条账密登录档案，对应上游 <c>EasiAutomation</c>。
/// </summary>
/// <remarks>
/// 类名按文档 §5.2 改为 <c>AutomationProfile</c>，避免沿用上游品牌命名；
/// JSON 字段名保持完全一致。
/// </remarks>
public sealed class AutomationProfile
{
    /// <summary>档案名称。</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>是否启用。</summary>
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>档案 ID（UUID 字符串）。</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>登录类型，当前仅 <c>password</c>。</summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "password";

    /// <summary>账号（启用加密时落盘为 <c>ea$</c> 密文）。</summary>
    [JsonPropertyName("account")]
    public string Account { get; set; } = string.Empty;

    /// <summary>密码（启用加密时落盘为 <c>ea$</c> 密文）。</summary>
    [JsonPropertyName("password")]
    public string Password { get; set; } = string.Empty;

    /// <summary>希沃白板用户名。</summary>
    [JsonPropertyName("account_name")]
    public string? AccountName { get; set; }

    /// <summary>
    /// 希沃白板头像。上游类型是 <c>Any</c>（可能是字符串路径，也可能是对象），
    /// 因此这里保留原始 JSON 容忍读取，不强行转成字符串。
    /// </summary>
    [JsonPropertyName("avatar")]
    public JsonElement? Avatar { get; set; }

    /// <summary>希沃白板用户 ID（本地比对用，免联网）。</summary>
    [JsonPropertyName("login_uid")]
    public string? LoginUid { get; set; }

    /// <summary>头像若为字符串则返回该路径，否则 <see langword="null"/>。</summary>
    [JsonIgnore]
    public string? AvatarPath
        => Avatar is { ValueKind: JsonValueKind.String } element ? element.GetString() : null;

    /// <summary>列表中显示的名称：优先备注名，其次用户名，最后账号。</summary>
    [JsonIgnore]
    public string DisplayLabel
        => !string.IsNullOrWhiteSpace(Name) ? Name!
            : !string.IsNullOrWhiteSpace(AccountName) ? AccountName!
            : !string.IsNullOrWhiteSpace(Account) ? Account
            : "(未命名)";
}

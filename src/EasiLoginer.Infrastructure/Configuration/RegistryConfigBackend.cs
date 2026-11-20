using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Win32;

namespace EasiLoginer.Infrastructure.Configuration;

/// <summary>
/// 注册表配置后端：每个设置项对应 <c>HKCU\Software\EasiLoginer\Settings</c> 下的一个值。
/// </summary>
/// <remarks>
/// 值名使用配置路径（如 <c>Login.Method</c>、<c>Appearance.BlurStrength</c>），值为该设置的 JSON 字面量。
/// 数组（如坐标）作为一个值保存。旧版本写入的单个 <c>Config</c> JSON 值会在首次读取后自动拆分。
/// </remarks>
public sealed class RegistryConfigBackend : IConfigBackend
{
    /// <summary>默认注册表路径。</summary>
    public const string DefaultKeyPath = @"Software\EasiLoginer\Settings";

    /// <summary>旧版整份配置值名称。</summary>
    public const string LegacyValueName = "Config";

    /// <summary>损坏备份值名称。</summary>
    public const string BackupValueName = "Config.bak";

    /// <summary>格式版本值名称。</summary>
    public const string VersionValueName = "SettingsVersion";

    private const string CurrentVersion = "2";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
    };

    private readonly string _keyPath;

    /// <summary>创建注册表后端。</summary>
    /// <param name="keyPath">测试可传入独立的 HKCU 子键，避免污染真实配置。</param>
    public RegistryConfigBackend(string keyPath = DefaultKeyPath)
    {
        if (string.IsNullOrWhiteSpace(keyPath))
        {
            throw new ArgumentException("注册表路径不能为空。", nameof(keyPath));
        }

        _keyPath = keyPath;
    }

    /// <summary>是否还是旧版单值格式（需要在下次保存时拆分）。</summary>
    public bool HasLegacyBlob
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(_keyPath, writable: false);
            return key?.GetValue(LegacyValueName) is string
                   && !string.Equals(key.GetValue(VersionValueName) as string, CurrentVersion, StringComparison.Ordinal);
        }
    }

    /// <inheritdoc />
    public bool Exists
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(_keyPath, writable: false);
            return key is not null &&
                   (key.GetValue(VersionValueName) is string ||
                    key.GetValue(LegacyValueName) is string ||
                    key.GetValueNames().Any(name => IsSettingValueName(name)));
        }
    }

    /// <inheritdoc />
    public string Read()
    {
        using var key = Registry.CurrentUser.OpenSubKey(_keyPath, writable: false)
                        ?? throw new IOException($"注册表配置键不存在：HKCU\\{_keyPath}");

        // 旧格式优先原样返回，ConfigStore 会把它当作一份完整配置读取。
        if (key.GetValue(LegacyValueName) is string legacy &&
            !string.Equals(key.GetValue(VersionValueName) as string, CurrentVersion, StringComparison.Ordinal))
        {
            return legacy;
        }

        var root = new JsonObject();
        foreach (var name in key.GetValueNames())
        {
            if (!IsSettingValueName(name) ||
                key.GetValue(name) is not string text)
            {
                continue;
            }

            JsonNode? node;
            try
            {
                node = JsonNode.Parse(text);
            }
            catch (JsonException exception)
            {
                throw new IOException($"注册表设置 {name} 不是合法 JSON。", exception);
            }

            SetPath(root, name, node);
        }

        return root.ToJsonString();
    }

    /// <inheritdoc />
    public void Write(string json)
    {
        var root = JsonNode.Parse(json) as JsonObject
                   ?? throw new JsonException("配置根节点必须是 JSON 对象。");

        var flat = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
        Flatten(root, string.Empty, flat);

        using var key = Registry.CurrentUser.CreateSubKey(_keyPath, writable: true)
                        ?? throw new IOException($"无法创建注册表配置键：HKCU\\{_keyPath}");

        // 删除旧版整份值；保留 Config.bak 备份。
        if (key.GetValue(LegacyValueName) is not null)
        {
            key.DeleteValue(LegacyValueName, throwOnMissingValue: false);
        }

        foreach (var name in key.GetValueNames())
        {
            if (name.Equals(BackupValueName, StringComparison.Ordinal) ||
                name.Equals(VersionValueName, StringComparison.Ordinal))
            {
                continue;
            }

            if (IsSettingValueName(name) && !flat.ContainsKey(name))
            {
                key.DeleteValue(name, throwOnMissingValue: false);
            }
        }

        foreach (var (name, node) in flat)
        {
            var value = node is null ? "null" : node.ToJsonString(JsonOptions);
            key.SetValue(name, value, RegistryValueKind.String);
        }

        key.SetValue(VersionValueName, CurrentVersion, RegistryValueKind.String);
    }

    /// <summary>删除整个配置键（重置数据时调用）。</summary>
    public void Clear() => Registry.CurrentUser.DeleteSubKeyTree(_keyPath, throwOnMissingSubKey: false);

    /// <inheritdoc />
    public void BackupCorrupt()
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(_keyPath, writable: true);
            key?.SetValue(BackupValueName, Read(), RegistryValueKind.String);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            // 备份失败不能阻止用默认配置启动
        }
    }

    private static bool IsSettingValueName(string name)
        => !string.IsNullOrEmpty(name) &&
           !name.Equals(LegacyValueName, StringComparison.Ordinal) &&
           !name.Equals(BackupValueName, StringComparison.Ordinal) &&
           !name.Equals(VersionValueName, StringComparison.Ordinal);

    private static void Flatten(JsonNode? node, string prefix, IDictionary<string, JsonNode?> target)
    {
        if (node is JsonObject obj)
        {
            foreach (var (name, value) in obj)
            {
                var path = string.IsNullOrEmpty(prefix) ? name : prefix + "." + name;
                Flatten(value, path, target);
            }

            return;
        }

        target[prefix] = node?.DeepClone();
    }

    private static void SetPath(JsonObject root, string path, JsonNode? value)
    {
        var parts = path.Split('.');
        var current = root;

        for (var index = 0; index < parts.Length - 1; index++)
        {
            if (current[parts[index]] is not JsonObject child)
            {
                child = new JsonObject();
                current[parts[index]] = child;
            }

            current = child;
        }

        current[parts[^1]] = value;
    }
}

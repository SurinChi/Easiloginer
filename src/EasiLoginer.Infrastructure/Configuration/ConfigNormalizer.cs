using System.Text.Json.Nodes;
using EasiLoginer.Domain.Configuration;

namespace EasiLoginer.Infrastructure.Configuration;

/// <summary>
/// 读取旧配置时的归一化：丢弃已移除的段、迁移旧键名、修正非法枚举值。
/// </summary>
/// <remarks>
/// 设计意图：把"兼容旧数据"集中在一个纯函数里，<see cref="Config"/> 保持干净，
/// 且每条规则都能被单测直接验证（文档 §11.1）。
/// </remarks>
public static class ConfigNormalizer
{
    private static readonly string[] ThemeValues = ["Auto", "Light", "Dark"];
    private static readonly string[] ChannelValues = ["release", "dev"];
    private static readonly string[] SourceValues = ["auto", "github", "ghproxy", "ghfast"];

    /// <summary>就地归一化配置 JSON。</summary>
    public static void Normalize(JsonObject root)
    {
        ArgumentNullException.ThrowIfNull(root);

        // 1. ClassIsland 整段丢弃，且不回写（§2.5 / §5.1）
        root.Remove("ClassIsland");

        // 2. 已随 ClassIsland 移除的内部标记
        if (root["Internal"] is JsonObject internalNode)
        {
            internalNode.Remove("IsAutomationPageNoticeShown");
        }

        // 3. 旧键名迁移
        RenameAlias(root["Login"] as JsonObject, "ForceEnableScaling", "ForceCompatibilityMode");
        RenameAlias(root["Warning"] as JsonObject, "Delay", "DelayTime");

        // 4. 未知枚举字符串回退默认值（§7.2「枚举未知值回退默认 + 日志」）
        CoerceStringEnum(root, "App", "Theme", ThemeValues, "Auto");
        CoerceStringEnum(root, "Update", "TargetUpdateChannel", ChannelValues, "release");
        CoerceStringEnum(root, "Update", "TargetDownloadSource", SourceValues, "auto");

        // 5. 值为 null 的段视为缺失，让默认值生效
        RemoveNullSections(root);
    }

    private static void RenameAlias(JsonObject? section, string legacyName, string currentName)
    {
        if (section is null || !section.TryGetPropertyValue(legacyName, out var value))
        {
            return;
        }

        section.Remove(legacyName);
        if (!section.ContainsKey(currentName))
        {
            section[currentName] = value?.DeepClone();
        }
    }

    private static void CoerceStringEnum(JsonObject root, string section, string property, string[] allowed, string fallback)
    {
        if (root[section] is not JsonObject sectionNode ||
            !sectionNode.TryGetPropertyValue(property, out var value))
        {
            return;
        }

        var text = value is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var candidate)
            ? candidate
            : null;

        if (text is null || !allowed.Contains(text, StringComparer.Ordinal))
        {
            sectionNode[property] = fallback;
        }
    }

    private static void RemoveNullSections(JsonObject root)
    {
        foreach (var name in root.Select(pair => pair.Key).ToArray())
        {
            if (root[name] is null)
            {
                root.Remove(name);
            }
        }
    }
}

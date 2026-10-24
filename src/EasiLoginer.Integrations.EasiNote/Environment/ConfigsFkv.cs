using System.Text;
using System.Text.RegularExpressions;

namespace EasiLoginer.Integrations.EasiNote.Environment;

/// <summary>
/// 希沃白板 <c>Configs.fkv</c> 的读取，对应上游 <c>env.read_config_value</c>。
/// </summary>
/// <remarks>
/// <para>
/// 文件是"键行 + 若干值行"的纯文本记录，记录之间以<b>单独一行</b> <c>&gt;</c> 分隔；
/// 其他以 <c>&gt;</c> 开头的行是注释。
/// </para>
/// <para>
/// <b>必须照抄的怪癖</b>：只有遇到下一个 <c>&gt;</c> 分隔行时才会返回上一条记录，
/// 因此文件末尾<b>没有</b>分隔行的最后一条记录是读不到的。上游就是这种行为，
/// 改动会让判定结果与上游不一致。
/// </para>
/// </remarks>
public static class ConfigsFkv
{
    /// <summary>设备类型覆写键。</summary>
    public const string DeviceInfoTypeKey = "DeviceInfo.Type";

    /// <summary>设备缓存键。</summary>
    public const string DeviceCacheIsIwbKey = "DeviceCache.IsIwb";

    /// <summary>IWB 设备类型 GUID。</summary>
    public const string IwbDeviceGuid = "228756AD-0620-497E-94FB-4022DB99C853";

    /// <summary>普通电脑设备类型 GUID。</summary>
    public const string PcDeviceGuid = "07BFE486-A99E-430C-8CCE-8A9955417614";

    /// <summary>默认配置文件路径：<c>%APPDATA%\Seewo\EasiNote5\Data\Configs.fkv</c>。</summary>
    public static string DefaultPath
    {
        get
        {
            var appData = System.Environment.GetEnvironmentVariable("APPDATA");
            return string.IsNullOrEmpty(appData)
                ? string.Empty
                : System.IO.Path.Combine(appData, "Seewo", "EasiNote5", "Data", "Configs.fkv");
        }
    }

    /// <summary>从文本中读取指定键的值（可能多行）；不存在返回 <see langword="null"/>。</summary>
    public static string? ReadValue(string text, string name)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(name);

        var lines = new List<string>();

        foreach (var line in SplitLines(text))
        {
            if (line == ">")
            {
                if (lines.Count >= 2 && string.Equals(lines[0], name, StringComparison.Ordinal))
                {
                    return string.Join('\n', lines.Skip(1));
                }

                lines.Clear();
            }
            else if (line.StartsWith('>'))
            {
                continue;
            }
            else
            {
                lines.Add(line);
            }
        }

        return null;
    }

    /// <summary>从文件读取指定键；文件缺失或读取失败返回 <see langword="null"/>。</summary>
    public static string? ReadValueFromFile(string path, string name)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            return null;
        }

        try
        {
            // 与上游一致：UTF-8 with BOM，非法字节替换为 U+FFFD 而不是抛错
            var text = File.ReadAllText(path, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            return ReadValue(text.TrimStart('\uFEFF'), name);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string[] SplitLines(string text)
        => LineBreakPattern.Split(text);

    private static readonly Regex LineBreakPattern = new(@"\r\n|\r|\n", RegexOptions.Compiled);
}

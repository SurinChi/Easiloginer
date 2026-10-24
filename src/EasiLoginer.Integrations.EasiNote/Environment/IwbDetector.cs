namespace EasiLoginer.Integrations.EasiNote.Environment;

/// <summary>
/// IWB（白板）启动环境判定，对应上游 <c>integrations/easinote/env.py</c>。
/// </summary>
/// <remarks>
/// <para>判定顺序固定，后者覆盖前者：</para>
/// <list type="number">
///   <item><c>Configs.fkv</c> 中 <c>DeviceInfo.Type</c> 的 GUID 覆写（IWB / PC）；</item>
///   <item>在线枚举到的希沃系 HID 触摸 MCU；</item>
///   <item><c>Configs.fkv</c> 中 <c>DeviceCache.IsIwb</c> 的历史缓存。</item>
/// </list>
/// <para>
/// 启动参数 <c>-m Display iwb</c> / <c>-m edit</c> 优先于以上全部判定
/// （<see cref="ResolveIsIwb"/>）。
/// </para>
/// </remarks>
public sealed class IwbDetector
{
    /// <summary>启动模式参数名。</summary>
    public static IReadOnlySet<string> ModeOptions => EasiLoginer.Domain.Automation.LaunchModeParser.ModeOptions;

    private readonly IEasiNoteConfigSource _configSource;
    private readonly IHidDeviceEnumerator _hidEnumerator;

    /// <summary>创建判定器。</summary>
    public IwbDetector(IEasiNoteConfigSource configSource, IHidDeviceEnumerator hidEnumerator)
    {
        ArgumentNullException.ThrowIfNull(configSource);
        ArgumentNullException.ThrowIfNull(hidEnumerator);

        _configSource = configSource;
        _hidEnumerator = hidEnumerator;
    }

    /// <summary>按机器环境判定是否进入白板路径。</summary>
    public bool Detect()
    {
        var deviceType = (_configSource.ReadValue(ConfigsFkv.DeviceInfoTypeKey) ?? string.Empty)
            .Trim()
            .Trim('{', '}')
            .ToUpperInvariant();

        if (string.Equals(deviceType, ConfigsFkv.IwbDeviceGuid, StringComparison.Ordinal))
        {
            return true;
        }

        if (string.Equals(deviceType, ConfigsFkv.PcDeviceGuid, StringComparison.Ordinal))
        {
            return false;
        }

        foreach (var path in _hidEnumerator.EnumerateDevicePaths())
        {
            if (new McuDevice(path).IsSeewoMcu())
            {
                return true;
            }
        }

        var cached = (_configSource.ReadValue(ConfigsFkv.DeviceCacheIsIwbKey) ?? string.Empty).Trim();
        return string.Equals(cached, "true", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>解析启动参数中显式指定的启动模式（小写）；未指定返回 <see langword="null"/>。</summary>
    public static string? ParseStartMode(string args) => EasiLoginer.Domain.Automation.LaunchModeParser.Parse(args);

    /// <summary>判断以指定参数启动后是否进入白板路径：显式 <c>-m</c> 优先，否则按机器环境。</summary>
    public bool ResolveIsIwb(string args = "")
    {
        var mode = ParseStartMode(args);

        if (string.Equals(mode, "display", StringComparison.Ordinal))
        {
            return true;
        }

        if (string.Equals(mode, "edit", StringComparison.Ordinal))
        {
            return false;
        }

        return Detect();
    }
}

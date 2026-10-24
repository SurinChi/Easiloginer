using System.Text.RegularExpressions;

namespace EasiLoginer.Integrations.EasiNote.Environment;

/// <summary>
/// 一台候选 HID 设备，对应上游 <c>env.McuDevice</c>（Cvte <c>UsbId</c> 的等价物）。
/// </summary>
public sealed class McuDevice
{
    /// <summary>设备接口路径示例：<c>\\?\hid#vid_1ff7&amp;pid_0f33&amp;mi_00#...</c>。</summary>
    private static readonly Regex DevicePathPattern = new(
        @"vid_([0-9a-f]{4})&pid_([0-9a-f]{4})(?:&([^#]+))?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>希沃系 USB 厂商号（McuHunter.Vids）。</summary>
    public static readonly IReadOnlySet<int> SeewoVendorIds = new HashSet<int> { 0x1FF7, 0x222A };

    /// <summary>PID 区间（闭区间，McuHunter.PidSubsection）。</summary>
    public static readonly IReadOnlyList<(int Low, int High)> SeewoProductIdRanges =
    [
        (1, 32),
        (3872, 3888),
        (3890, 3903),
    ];

    /// <summary>接口键提示（McuHunter.PchKey）。</summary>
    public static readonly IReadOnlyList<string> SeewoKeyHints = ["mi_00", "col03"];

    /// <summary>
    /// 定点设备表，元组语义为 <c>(pid, vid, key)</c>。
    /// </summary>
    /// <remarks>
    /// <b>逐条照抄，不要"顺手修正"顺序</b>（文档 §8.2）：表中部分条目的数字看起来像
    /// <c>(vid, pid)</c>，但上游按 <c>(pid, vid, key)</c> 比较，照抄才能与上游判定一致。
    /// </remarks>
    public static readonly IReadOnlyList<(int Pid, int Vid, string Key)> SeewoDevices =
    [
        (0x1FF7, 0x0F15, "mi_00&col02"), // CommonMcu
        (0x0F15, 0x1FF7, "mi_00"),       // Mcu638 / Mcu551
        (0x0001, 0x1FF7, "col03"),       // McuTouch
        (0xAA55, 0x10E0, "col01"),       // Mcu309，老式 CV 触摸框
        (0x0F21, 0x1FF7, "mi_00&col02"), // Mcu551B2
        (0x0F26, 0x1FF7, "mi_00"),       // McuV
        (0x0F50, 0x1FF7, "mi_00"),       // McuFourSidesInfraredBlackboard
        (0x0F28, 0x1FF7, "mi_00"),       // McuFlatFrogTouchFrame
        (0x0F27, 0x1FF7, "mi_00"),       // McuVIAbroad
        (0x0F33, 0x1FF7, "mi_00"),       // McuVI / McuVII 四边红外系列
        (0x0F33, 0x1FF7, ""),            // McuVII / McuVIII，仅以 REV 区分型号
    ];

    /// <summary>解析设备接口路径。</summary>
    public McuDevice(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        Path = path;
        var match = DevicePathPattern.Match(path);
        if (match.Success)
        {
            Vid = int.Parse(match.Groups[1].Value, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture);
            Pid = int.Parse(match.Groups[2].Value, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture);
            Key = match.Groups[3].Success ? match.Groups[3].Value : string.Empty;
        }
        else
        {
            Vid = -1;
            Pid = -1;
            Key = string.Empty;
        }
    }

    /// <summary>完整设备接口路径。</summary>
    public string Path { get; }

    /// <summary>USB 厂商号；解析失败为 -1。</summary>
    public int Vid { get; }

    /// <summary>USB 产品号；解析失败为 -1。</summary>
    public int Pid { get; }

    /// <summary>接口键名，可能为空。</summary>
    public string Key { get; }

    /// <summary>是否与给定特征一致；<paramref name="key"/> 为空表示只比较 PID/VID。</summary>
    public bool Matches(int pid, int vid, string key)
    {
        if (Pid != pid || Vid != vid)
        {
            return false;
        }

        if (string.IsNullOrEmpty(key))
        {
            return true;
        }

        return Key.Contains(key, StringComparison.OrdinalIgnoreCase) ||
               Path.Contains(key, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>是否为希沃系触摸 MCU（等价于 McuHunter.FindAll 的结果非空）。</summary>
    public bool IsSeewoMcu()
    {
        foreach (var (pid, vid, key) in SeewoDevices)
        {
            if (Matches(pid, vid, key))
            {
                return true;
            }
        }

        if (!SeewoVendorIds.Contains(Vid))
        {
            return false;
        }

        var inRange = false;
        foreach (var (low, high) in SeewoProductIdRanges)
        {
            if (Pid >= low && Pid <= high)
            {
                inRange = true;
                break;
            }
        }

        if (!inRange)
        {
            return false;
        }

        foreach (var hint in SeewoKeyHints)
        {
            if (Key.Contains(hint, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <inheritdoc />
    public override string ToString() => $"McuDevice(pid=0x{Pid:X4}, vid=0x{Vid:X4}, key={Key})";
}

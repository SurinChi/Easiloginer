namespace EasiLoginer.Presentation.Configuration;

/// <summary>设置项编辑器类型。</summary>
public enum SettingKind
{
    /// <summary>开关。</summary>
    Boolean,

    /// <summary>文本。</summary>
    Text,

    /// <summary>字体：下拉选择系统已安装字体。</summary>
    Font,

    /// <summary>数值（含小数）。</summary>
    Number,

    /// <summary>枚举下拉。</summary>
    Enum,

    /// <summary>坐标（<c>x, y</c>）。</summary>
    Point,

    /// <summary>颜色（<c>#AARRGGBB</c>）。</summary>
    Color,
}

/// <summary>一个设置项的静态描述（不含值）。</summary>
/// <param name="Path">相对 <c>Config</c> 的点分路径。</param>
/// <param name="Group">所属分组键。</param>
/// <param name="Title">显示标题。</param>
/// <param name="Kind">编辑器类型。</param>
/// <param name="Options">枚举项显示文本（仅枚举）。</param>
/// <param name="Min">数值下限（仅数值）。</param>
/// <param name="Max">数值上限（仅数值）。</param>
public sealed record SettingDescriptor(
    string Path,
    string Group,
    string Title,
    SettingKind Kind,
    IReadOnlyList<string>? Options = null,
    double? Min = null,
    double? Max = null);

/// <summary>一个设置分组（显示用）。</summary>
/// <param name="Key">分组键。</param>
/// <param name="Title">标题。</param>
public sealed record SettingGroup(string Key, string Title);

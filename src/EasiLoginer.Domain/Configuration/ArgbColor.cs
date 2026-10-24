using System.Globalization;

namespace EasiLoginer.Domain.Configuration;

/// <summary>
/// ARGB 颜色值对象，对应 Python 侧 <c>QColor</c> 在配置中的字符串形式。
/// </summary>
/// <remarks>
/// 上游用 qt_pydantic 把 <c>QColor</c> 序列化成 <c>#AARRGGBB</c>（8 位十六进制，
/// 首位是 alpha），默认值如 <c>#FFFFDE59</c>。Domain 不引入 Qt/WPF 类型，
/// 因此以本值对象承载，具体渲染在呈现层转换。
/// </remarks>
public readonly record struct ArgbColor(byte A, byte R, byte G, byte B)
{
    /// <summary>解析 <c>#AARRGGBB</c> 或 <c>#RRGGBB</c>（后者按不透明处理）。</summary>
    /// <exception cref="FormatException">格式无法识别。</exception>
    public static ArgbColor Parse(string text)
    {
        if (!TryParse(text, out var color))
        {
            throw new FormatException($"无法解析颜色值：'{text}'。");
        }

        return color;
    }

    /// <summary>尝试解析颜色字符串。</summary>
    public static bool TryParse(string? text, out ArgbColor color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var value = text.Trim();
        if (value.StartsWith('#'))
        {
            value = value[1..];
        }

        if (value.Length is not (6 or 8) ||
            !uint.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var packed))
        {
            return false;
        }

        color = value.Length == 8
            ? new ArgbColor((byte)(packed >> 24), (byte)(packed >> 16), (byte)(packed >> 8), (byte)packed)
            : new ArgbColor(0xFF, (byte)(packed >> 16), (byte)(packed >> 8), (byte)packed);

        return true;
    }

    /// <summary>输出 <c>#AARRGGBB</c> 形式（大写，与上游默认值写法一致）。</summary>
    public override string ToString()
        => string.Create(CultureInfo.InvariantCulture, $"#{A:X2}{R:X2}{G:X2}{B:X2}");
}

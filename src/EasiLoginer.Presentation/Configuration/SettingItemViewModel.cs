using System.Globalization;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using EasiLoginer.Domain.Configuration;
using EasiLoginer.Domain.Geometry;

namespace EasiLoginer.Presentation.Configuration;

/// <summary>
/// 单个设置卡片：持有路径、编辑器状态，并在保存时写回 <see cref="Config"/>。
/// </summary>
public sealed partial class SettingItemViewModel : ObservableObject
{
    private readonly Config _config;
    private readonly object _owner;
    private readonly PropertyInfo _leaf;

    [ObservableProperty]
    private bool _boolValue;

    [ObservableProperty]
    private string _textValue = string.Empty;

    [ObservableProperty]
    private int _selectedIndex;

    /// <summary>创建设置项并载入当前值。</summary>
    public SettingItemViewModel(Config config, SettingDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(descriptor);

        _config = config;
        Descriptor = descriptor;

        var parts = descriptor.Path.Split('.');
        object owner = config;
        for (var index = 0; index < parts.Length - 1; index++)
        {
            owner = owner.GetType().GetProperty(parts[index])!.GetValue(owner)!;
        }

        _owner = owner;
        _leaf = owner.GetType().GetProperty(parts[^1])!;

        Load();
    }

    /// <summary>静态描述。</summary>
    public SettingDescriptor Descriptor { get; }

    /// <summary>标题。</summary>
    public string Title => Descriptor.Title;

    /// <summary>是否布尔开关。</summary>
    public bool IsBool => Descriptor.Kind == SettingKind.Boolean;

    /// <summary>是否枚举下拉。</summary>
    public bool IsEnum => Descriptor.Kind == SettingKind.Enum;

    /// <summary>是否文本输入（文本/数值/坐标/颜色）。</summary>
    public bool IsTextual => Descriptor.Kind is SettingKind.Text or SettingKind.Number or SettingKind.Point or SettingKind.Color;

    /// <summary>枚举显示文本。</summary>
    public IReadOnlyList<string> Options => Descriptor.Options ?? [];

    /// <summary>把当前值写回配置；格式非法时抛 <see cref="FormatException"/>。</summary>
    public void Apply()
    {
        switch (Descriptor.Kind)
        {
            case SettingKind.Boolean:
                _leaf.SetValue(_owner, BoolValue);
                break;

            case SettingKind.Enum:
                _leaf.SetValue(_owner, Enum.ToObject(_leaf.PropertyType, SelectedIndex));
                break;

            case SettingKind.Number:
                var number = double.Parse(TextValue, CultureInfo.InvariantCulture);
                if (Descriptor.Min is { } min && number < min)
                {
                    number = min;
                }

                if (Descriptor.Max is { } max && number > max)
                {
                    number = max;
                }

                _leaf.SetValue(_owner, Convert.ChangeType(number, _leaf.PropertyType, CultureInfo.InvariantCulture));
                TextValue = number.ToString(CultureInfo.InvariantCulture);
                break;

            case SettingKind.Point:
                _leaf.SetValue(_owner, ParsePoint(TextValue));
                break;

            case SettingKind.Color:
                _leaf.SetValue(_owner, ArgbColor.Parse(TextValue));
                break;

            default:
                _leaf.SetValue(_owner, TextValue);
                break;
        }
    }

    private void Load()
    {
        var value = _leaf.GetValue(_owner);

        switch (Descriptor.Kind)
        {
            case SettingKind.Boolean:
                BoolValue = value is true;
                break;

            case SettingKind.Enum:
                SelectedIndex = value is null ? 0 : Convert.ToInt32(value, CultureInfo.InvariantCulture);
                break;

            case SettingKind.Number:
                TextValue = Convert.ToDouble(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
                break;

            case SettingKind.Point:
                // 与 ParsePoint 的格式保持一致，避免"没动过也保存失败"
                TextValue = value is PixelPoint point ? $"{point.X}, {point.Y}" : string.Empty;
                break;

            default:
                TextValue = value?.ToString() ?? string.Empty;
                break;
        }
    }

    private static PixelPoint ParsePoint(string text)
    {
        // 兼容 "(172, 1044)"、"[172, 1044]"、"172,1044" 以及全角逗号
        var normalized = text.Trim().Trim('(', ')', '[', ']', '（', '）', '【', '】').Trim();
        var parts = normalized.Split(
            [',', '，', ';', '；'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length != 2 ||
            !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var x) ||
            !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var y))
        {
            throw new FormatException($"坐标格式应为 \"x, y\"，实际为 \"{text}\"。");
        }

        return new PixelPoint(x, y);
    }
}

/// <summary>一个设置分组及其卡片。</summary>
public sealed class SettingsGroupViewModel(string title, IReadOnlyList<SettingItemViewModel> items)
{
    /// <summary>分组标题。</summary>
    public string Title { get; } = title;

    /// <summary>分组内的设置项。</summary>
    public IReadOnlyList<SettingItemViewModel> Items { get; } = items;
}

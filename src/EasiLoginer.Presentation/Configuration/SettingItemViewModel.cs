using System.Globalization;
using System.IO;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

    [ObservableProperty]
    private double _numberValue;

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

    /// <summary>标题下方的二级说明（没有则为空）。</summary>
    public string Hint => Descriptor.Hint ?? string.Empty;

    /// <summary>是否有二级说明。</summary>
    public bool HasHint => !string.IsNullOrEmpty(Descriptor.Hint);

    /// <summary>是否布尔开关。</summary>
    public bool IsBool => Descriptor.Kind == SettingKind.Boolean;

    /// <summary>是否枚举下拉。</summary>
    public bool IsEnum => Descriptor.Kind == SettingKind.Enum;

    /// <summary>是否字体下拉。</summary>
    public bool IsFont => Descriptor.Kind == SettingKind.Font;

    /// <summary>是否滑杆。</summary>
    public bool IsSlider => Descriptor.Kind == SettingKind.Slider;

    /// <summary>是否文件路径（带「浏览…」按钮）。</summary>
    public bool IsFilePath => Descriptor.Kind == SettingKind.FilePath;

    /// <summary>是否文本输入（文本/数值/坐标/颜色）。</summary>
    public bool IsTextual => Descriptor.Kind is SettingKind.Text or SettingKind.Number or SettingKind.Point or SettingKind.Color;

    /// <summary>滑杆下限。</summary>
    public double Minimum => Descriptor.Min ?? 0;

    /// <summary>滑杆上限。</summary>
    public double Maximum => Descriptor.Max ?? 100;

    /// <summary>滑杆当前值的显示文本。</summary>
    public string NumberText => NumberValue.ToString("0", CultureInfo.InvariantCulture);

    /// <summary>枚举显示文本。</summary>
    public IReadOnlyList<string> Options => Descriptor.Options ?? [];

    /// <summary>
    /// 字体下拉的选项：系统已安装字体；当前值不在其中时置于首位，避免把已配置的字体改掉。
    /// </summary>
    public IReadOnlyList<string> FontOptions
    {
        get
        {
            var current = TextValue;
            if (string.IsNullOrWhiteSpace(current) ||
                InstalledFonts.Contains(current, StringComparer.OrdinalIgnoreCase))
            {
                return InstalledFonts;
            }

            return [current, .. InstalledFonts];
        }
    }

    private static readonly string[] InstalledFonts = BuildInstalledFonts();

    private static string[] BuildInstalledFonts()
        => System.Windows.Media.Fonts.SystemFontFamilies
            .Select(family => family.Source)
            .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

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

            case SettingKind.Slider:
                var sliderValue = Math.Round(Math.Clamp(NumberValue, Minimum, Maximum));
                _leaf.SetValue(_owner, Convert.ChangeType(sliderValue, _leaf.PropertyType, CultureInfo.InvariantCulture));
                NumberValue = sliderValue;
                TextValue = NumberText;
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

    partial void OnNumberValueChanged(double value) => OnPropertyChanged(nameof(NumberText));

    /// <summary>打开文件选择框，把图片路径写回文本框。</summary>
    [RelayCommand]
    private void BrowseImage()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "选择背景图片",
            Filter = "图片文件|*.png;*.jpg;*.jpeg;*.bmp;*.webp;*.gif|所有文件|*.*",
            CheckFileExists = true,
        };

        if (!string.IsNullOrWhiteSpace(TextValue) && File.Exists(TextValue))
        {
            dialog.FileName = TextValue;
        }

        var owner = System.Windows.Application.Current?.MainWindow;
        var accepted = owner is null ? dialog.ShowDialog() == true : dialog.ShowDialog(owner) == true;
        if (accepted)
        {
            TextValue = dialog.FileName;
        }
    }

    /// <summary>把编辑器恢复为该设置项的默认值（尚未写回配置，需点击保存）。</summary>
    public void ResetToDefault() => LoadValue(ReadValue(new Config(), Descriptor.Path));

    private void Load() => LoadValue(_leaf.GetValue(_owner));

    private void LoadValue(object? value)
    {
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

            case SettingKind.Slider:
                NumberValue = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                TextValue = NumberText;
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

    private static object? ReadValue(Config config, string path)
    {
        var parts = path.Split('.');
        object? owner = config;
        for (var index = 0; index < parts.Length; index++)
        {
            if (owner is null)
            {
                return null;
            }

            owner = owner.GetType().GetProperty(parts[index])?.GetValue(owner);
        }

        return owner;
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

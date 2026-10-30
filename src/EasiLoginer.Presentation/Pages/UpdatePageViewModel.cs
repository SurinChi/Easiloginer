using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using EasiLoginer.Application.Abstractions;
using EasiLoginer.Presentation.Configuration;

namespace EasiLoginer.Presentation.Pages;

/// <summary>更新页视图模型：只承载「更新设置」分组（配置页不再显示它）。</summary>
public sealed partial class UpdatePageViewModel : ObservableObject
{
    private readonly IConfigService _config;

    [ObservableProperty]
    private string _settingsStatus = string.Empty;

    /// <summary>创建视图模型。</summary>
    public UpdatePageViewModel(IConfigService config)
    {
        ArgumentNullException.ThrowIfNull(config);
        _config = config;

        Groups = ConfigCatalog.Build()
            .Where(descriptor => string.Equals(descriptor.Group, "Update", StringComparison.Ordinal))
            .GroupBy(descriptor => descriptor.Group)
            .Select(group => new SettingsGroupViewModel(
                ConfigCatalog.GetGroupTitle(group.Key),
                group.Select(descriptor => new SettingItemViewModel(config.Current, descriptor)).ToList()))
            .ToList();
    }

    /// <summary>「更新设置」分组。</summary>
    public IReadOnlyList<SettingsGroupViewModel> Groups { get; }

    /// <summary>把更新设置写回配置并落盘。</summary>
    public void Save()
    {
        try
        {
            foreach (var item in Groups.SelectMany(group => group.Items))
            {
                item.Apply();
            }

            _config.Save();
            SettingsStatus = string.Create(CultureInfo.InvariantCulture, $"设置已保存（{DateTime.Now:HH:mm:ss}）");
        }
        catch (FormatException exception)
        {
            SettingsStatus = $"保存失败：{exception.Message}";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            SettingsStatus = $"保存失败：{exception.Message}";
        }
    }
}

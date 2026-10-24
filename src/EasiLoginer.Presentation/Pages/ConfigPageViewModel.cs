using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using EasiLoginer.Application.Abstractions;
using EasiLoginer.Presentation.Configuration;

namespace EasiLoginer.Presentation.Pages;

/// <summary>配置页视图模型：按 <see cref="ConfigCatalog"/> 生成全部分组与设置卡片。</summary>
public sealed partial class ConfigPageViewModel : ObservableObject
{
    private readonly IConfigService _config;

    [ObservableProperty]
    private string _statusText = string.Empty;

    /// <summary>创建视图模型并构建设置树。</summary>
    public ConfigPageViewModel(IConfigService config)
    {
        ArgumentNullException.ThrowIfNull(config);
        _config = config;

        Groups = ConfigCatalog.Build()
            .GroupBy(descriptor => descriptor.Group)
            .Select(group => new SettingsGroupViewModel(
                ConfigCatalog.GetGroupTitle(group.Key),
                group.Select(descriptor => new SettingItemViewModel(config.Current, descriptor)).ToList()))
            .ToList();
    }

    /// <summary>设置分组。</summary>
    public IReadOnlyList<SettingsGroupViewModel> Groups { get; }

    /// <summary>把界面值写回配置并落盘。</summary>
    public void Save()
    {
        try
        {
            foreach (var item in Groups.SelectMany(group => group.Items))
            {
                item.Apply();
            }

            _config.Save();
            ThemeService.Apply(_config.Current.App.Theme);
            StatusText = string.Create(CultureInfo.InvariantCulture, $"已保存（{DateTime.Now:HH:mm:ss}）");
        }
        catch (FormatException exception)
        {
            StatusText = $"保存失败：{exception.Message}";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            StatusText = $"保存失败：{exception.Message}";
        }
    }
}

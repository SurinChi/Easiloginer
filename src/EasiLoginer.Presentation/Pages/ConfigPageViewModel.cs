using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using EasiLoginer.Application.Abstractions;
using EasiLoginer.Presentation.Configuration;

namespace EasiLoginer.Presentation.Pages;

/// <summary>
/// 「设置」页视图模型：按 <see cref="ConfigCatalog"/> 生成全部分组与设置卡片，
/// 并按功能分成顶部导航的若干类。
/// </summary>
public sealed partial class ConfigPageViewModel : ObservableObject
{
    private readonly IConfigService _config;

    [ObservableProperty]
    private string _statusText = string.Empty;

    /// <summary>当前选中的功能分类（顶部导航的下标）。</summary>
    [ObservableProperty]
    private int _selectedCategoryIndex;

    /// <summary>创建视图模型并构建设置树。</summary>
    public ConfigPageViewModel(IConfigService config)
    {
        ArgumentNullException.ThrowIfNull(config);
        _config = config;

        var categories = ConfigCatalog.BuildCategories();

        Categories = categories
            .Select(category => new SettingsCategoryViewModel(
                category.Title,
                BuildGroups(config, category.Descriptors)))
            .ToList();

        // 供测试与「保存」遍历使用：所有分类下的全部分组
        Groups = Categories.SelectMany(category => category.Groups).ToList();
    }

    /// <summary>顶部分类导航。</summary>
    public IReadOnlyList<SettingsCategoryViewModel> Categories { get; }

    /// <summary>当前分类下的分组（未选中分类时为空）。</summary>
    public IReadOnlyList<SettingsGroupViewModel> CurrentGroups
        => SelectedCategoryIndex >= 0 && SelectedCategoryIndex < Categories.Count
            ? Categories[SelectedCategoryIndex].Groups
            : [];

    /// <summary>全部设置分组（按分类顺序平铺）。</summary>
    public IReadOnlyList<SettingsGroupViewModel> Groups { get; }

    partial void OnSelectedCategoryIndexChanged(int value) => OnPropertyChanged(nameof(CurrentGroups));

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

    private static List<SettingsGroupViewModel> BuildGroups(
        IConfigService config,
        IReadOnlyList<SettingDescriptor> descriptors)
        => descriptors
            .GroupBy(descriptor => descriptor.Group)
            .Select(group => new SettingsGroupViewModel(
                ConfigCatalog.GetGroupTitle(group.Key),
                group.Select(descriptor => new SettingItemViewModel(config.Current, descriptor)).ToList()))
            .ToList();
}

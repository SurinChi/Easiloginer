using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using EasiLoginer.Application.Abstractions;
using EasiLoginer.Presentation.Appearance;
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

    /// <summary>是否有尚未保存的改动（用于左下角悬浮提示）。</summary>
    [ObservableProperty]
    private bool _hasUnsavedChanges;

    private bool _suppressDirty;

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

        // 订阅放在初始载入之后，避免构建卡片时就被判定为“有改动”。
        foreach (var item in AllItems)
        {
            item.PropertyChanged += OnItemPropertyChanged;
        }
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

    private IEnumerable<SettingItemViewModel> AllItems => Groups.SelectMany(group => group.Items);

    partial void OnSelectedCategoryIndexChanged(int value) => OnPropertyChanged(nameof(CurrentGroups));

    private void OnItemPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (!_suppressDirty)
        {
            HasUnsavedChanges = true;
        }
    }

    /// <summary>把界面值写回配置并落盘。</summary>
    public void Save()
    {
        _suppressDirty = true;
        try
        {
            foreach (var item in AllItems)
            {
                item.Apply();
            }

            _config.Save();
            ThemeService.Apply(_config.Current.App.Theme);
            AppearanceService.Apply(_config.Current);
            HasUnsavedChanges = false;
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
        finally
        {
            _suppressDirty = false;
        }
    }

    /// <summary>把全部设置恢复默认值（尚未保存）。</summary>
    public void ResetAll()
    {
        foreach (var item in AllItems)
        {
            item.ResetToDefault();
        }

        HasUnsavedChanges = true;
        StatusText = "已重置全部设置，点击“保存”后生效。";
    }

    /// <summary>把当前分类的设置恢复默认值（尚未保存）。</summary>
    public void ResetCurrentPage()
    {
        foreach (var item in CurrentGroups.SelectMany(group => group.Items))
        {
            item.ResetToDefault();
        }

        HasUnsavedChanges = true;
        StatusText = "已重置本页设置，点击“保存”后生效。";
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

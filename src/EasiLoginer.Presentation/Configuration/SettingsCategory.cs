namespace EasiLoginer.Presentation.Configuration;

/// <summary>
/// 设置页的一个功能分类（顶部导航的一栏），只描述包含哪些设置项。
/// </summary>
/// <param name="Title">分类标题。</param>
/// <param name="Descriptors">分类下的设置项（保持 <see cref="ConfigCatalog"/> 中的顺序）。</param>
public sealed record SettingsCategory(string Title, IReadOnlyList<SettingDescriptor> Descriptors);

/// <summary>设置页的一个功能分类及其分组卡片（显示用）。</summary>
public sealed class SettingsCategoryViewModel(string title, IReadOnlyList<SettingsGroupViewModel> groups)
{
    /// <summary>分类标题（显示在顶部导航上）。</summary>
    public string Title { get; } = title;

    /// <summary>分类下的分组卡片。</summary>
    public IReadOnlyList<SettingsGroupViewModel> Groups { get; } = groups;

    /// <inheritdoc />
    public override string ToString() => Title;
}

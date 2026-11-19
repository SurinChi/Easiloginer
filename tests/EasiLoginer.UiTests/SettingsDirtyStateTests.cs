using EasiLoginer.Application.Abstractions;
using EasiLoginer.Domain.Configuration;
using EasiLoginer.Presentation.Pages;
using FluentAssertions;

namespace EasiLoginer.UiTests;

/// <summary>设置页的未保存提示、重置全部与重置本页。</summary>
public sealed class SettingsDirtyStateTests
{
    /// <summary>初始不显示提示；修改后显示；保存后隐藏。</summary>
    [Fact]
    public void 修改后标记未保存保存后清除()
    {
        var service = new FakeConfigService();
        var viewModel = new ConfigPageViewModel(service);
        viewModel.HasUnsavedChanges.Should().BeFalse();

        var method = Find(viewModel, "登录方式");
        method.SelectedIndex = (int)LoginMethod.Fixed;

        viewModel.HasUnsavedChanges.Should().BeTrue();
        viewModel.Save();

        viewModel.HasUnsavedChanges.Should().BeFalse();
        service.Current.Login.Method.Should().Be(LoginMethod.Fixed);
        service.SaveCalls.Should().Be(1);
    }

    /// <summary>重置本页只改当前分类的编辑器值，保存后才写回配置。</summary>
    [Fact]
    public void 重置本页只重置当前分类()
    {
        var service = new FakeConfigService();
        var viewModel = new ConfigPageViewModel(service);
        viewModel.SelectedCategoryIndex = 0;

        var method = Find(viewModel, "登录方式");
        method.SelectedIndex = (int)LoginMethod.Fixed;
        viewModel.ResetCurrentPage();

        method.SelectedIndex.Should().Be((int)LoginMethod.Uia, "登录方式默认是自动定位");
        viewModel.HasUnsavedChanges.Should().BeTrue();
        service.Current.Login.Method.Should().NotBe(LoginMethod.Fixed, "重置尚未保存");
    }

    /// <summary>重置全部把所有编辑器恢复默认，保存后写回默认配置。</summary>
    [Fact]
    public void 重置全部保存后恢复默认()
    {
        var service = new FakeConfigService();
        var viewModel = new ConfigPageViewModel(service);

        var method = Find(viewModel, "登录方式");
        method.SelectedIndex = (int)LoginMethod.Fixed;
        viewModel.ResetAll();
        viewModel.Save();

        method.SelectedIndex.Should().Be((int)LoginMethod.Uia);
        service.Current.Login.Method.Should().Be(LoginMethod.Uia);
        viewModel.HasUnsavedChanges.Should().BeFalse();
    }

    private static EasiLoginer.Presentation.Configuration.SettingItemViewModel Find(
        ConfigPageViewModel viewModel,
        string title)
        => viewModel.Groups.SelectMany(group => group.Items).Single(item => item.Title == title);

    private sealed class FakeConfigService : IConfigService
    {
        public Config Current { get; } = new();

        public int SaveCalls { get; private set; }

        public void Save() => SaveCalls++;
    }
}

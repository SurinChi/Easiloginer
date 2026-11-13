using EasiLoginer.Application.Abstractions;
using EasiLoginer.Domain.Configuration;
using EasiLoginer.Presentation.Pages;
using FluentAssertions;

namespace EasiLoginer.UiTests;

/// <summary>「设置」页的顶部分类导航：分类划分、默认选中与保存范围。</summary>
public sealed class SettingsCategoriesTests
{
    /// <summary>按功能分成三类，登录在最前。</summary>
    [Fact]
    public void 顶部分类按功能划分()
    {
        var viewModel = new ConfigPageViewModel(new FakeConfigService());

        viewModel.Categories.Select(category => category.Title)
            .Should().Equal("登录", "提醒与遮罩", "应用与外观");
    }

    /// <summary>每个分类都至少有一个分组，且全部设置项都在某个分类里。</summary>
    [Fact]
    public void 分类覆盖全部设置项()
    {
        var viewModel = new ConfigPageViewModel(new FakeConfigService());

        viewModel.Categories.Should().OnlyContain(category => category.Groups.Count > 0);

        var titles = viewModel.Categories
            .SelectMany(category => category.Groups)
            .SelectMany(group => group.Items)
            .Select(item => item.Title)
            .ToList();

        titles.Should().Contain(["登录方式", "超时时长（秒）", "文本", "应用主题"]);

        // 「更新设置」由更新页承载，不在设置页重复出现
        viewModel.Groups.Select(group => group.Title).Should().NotContain("更新设置");
    }

    /// <summary>默认选中第一个分类，内容区跟着切换。</summary>
    [Fact]
    public void 默认选中第一类()
    {
        var viewModel = new ConfigPageViewModel(new FakeConfigService());

        viewModel.SelectedCategoryIndex.Should().Be(0);
        viewModel.CurrentGroups.Should().BeSameAs(viewModel.Categories[0].Groups);
        viewModel.CurrentGroups.Select(group => group.Title).Should().Contain("登录选项");
    }

    /// <summary>切换分类后内容区换成该分类的分组。</summary>
    [Fact]
    public void 切换分类更换分组()
    {
        var viewModel = new ConfigPageViewModel(new FakeConfigService());

        viewModel.SelectedCategoryIndex = 1;

        viewModel.CurrentGroups.Select(group => group.Title)
            .Should().Contain(["警告弹窗", "警示横幅", "状态浮窗"]);
        viewModel.CurrentGroups.Select(group => group.Title).Should().NotContain("登录选项");
    }

    /// <summary>「保存」遍历所有分类，而不只是当前显示的那一类。</summary>
    [Fact]
    public void 保存覆盖全部分类()
    {
        var config = new Config();
        var service = new FakeConfigService(config);
        var viewModel = new ConfigPageViewModel(service);

        // 外观在第三个分类；先切到外观再改，最后仍要写回
        viewModel.SelectedCategoryIndex = 2;
        var theme = viewModel.CurrentGroups
            .SelectMany(group => group.Items)
            .Single(item => item.Title == "应用主题");
        theme.SelectedIndex = (int)ThemeOptions.Dark;

        viewModel.Save();

        config.App.Theme.Should().Be(ThemeOptions.Dark);
        service.SaveCalls.Should().Be(1);
    }

    private sealed class FakeConfigService(Config? config = null) : IConfigService
    {
        public Config Current { get; } = config ?? new Config();

        public int SaveCalls { get; private set; }

        public void Save() => SaveCalls++;
    }
}

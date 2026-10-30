using EasiLoginer.Application.Abstractions;
using EasiLoginer.Domain.Configuration;
using EasiLoginer.Presentation.Pages;
using FluentAssertions;

namespace EasiLoginer.UiTests;

/// <summary>「更新设置」分组从配置页迁到更新页之后的分组归属。</summary>
public sealed class SettingsGroupsTests
{
    /// <summary>配置页不再出现「更新设置」分组。</summary>
    [Fact]
    public void 配置页不包含更新分组()
    {
        var viewModel = new ConfigPageViewModel(new FakeConfigService());

        viewModel.Groups.Select(group => group.Title).Should().NotContain("更新设置");
    }

    /// <summary>更新页只承载「更新设置」分组，且包含四项设置。</summary>
    [Fact]
    public void 更新页包含更新分组()
    {
        var viewModel = new UpdatePageViewModel(new FakeConfigService());

        viewModel.Groups.Should().ContainSingle();
        viewModel.Groups[0].Title.Should().Be("更新设置");
        viewModel.Groups[0].Items.Select(item => item.Title)
            .Should().Contain(["更新模式", "登录后检查更新", "更新通道", "下载镜像源"]);
    }

    /// <summary>更新页保存会把设置写回配置。</summary>
    [Fact]
    public void 更新页保存写回配置()
    {
        var config = new Config();
        var service = new FakeConfigService(config);
        var viewModel = new UpdatePageViewModel(service);

        var modeItem = viewModel.Groups[0].Items.Single(item => item.Title == "更新模式");
        modeItem.SelectedIndex = (int)UpdateMode.CheckAndNotify;
        viewModel.Save();

        config.Update.Mode.Should().Be(UpdateMode.CheckAndNotify);
        service.SaveCalls.Should().Be(1);
    }

    private sealed class FakeConfigService(Config? config = null) : IConfigService
    {
        public Config Current { get; } = config ?? new Config();

        public int SaveCalls { get; private set; }

        public void Save() => SaveCalls++;
    }
}

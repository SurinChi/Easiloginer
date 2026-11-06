using EasiLoginer.Domain.Configuration;
using EasiLoginer.Presentation.Pages;
using EasiLoginer.Presentation.Shell;
using FluentAssertions;

namespace EasiLoginer.UiTests;

/// <summary>启动默认页：自动规则与用户指定页面。</summary>
public sealed class StartPagePolicyTests
{
    /// <summary>自动：有档案 → 快捷登录。</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void 自动有档案进快捷登录(int count)
    {
        StartPagePolicy.Resolve(StartPageOptions.Auto, count).Should().Be<QuickLoginPage>();
    }

    /// <summary>自动：没有档案 → 档案页。</summary>
    [Fact]
    public void 自动无档案进档案页()
    {
        StartPagePolicy.Resolve(StartPageOptions.Auto, 0).Should().Be<ProfilePage>();
    }

    /// <summary>用户显式指定时忽略档案数量。</summary>
    [Theory]
    [InlineData(StartPageOptions.QuickLogin, 0)]
    [InlineData(StartPageOptions.Profile, 5)]
    [InlineData(StartPageOptions.Config, 2)]
    public void 指定页面优先(StartPageOptions option, int count)
    {
        StartPagePolicy.Resolve(option, count).Should().Be(
            option switch
            {
                StartPageOptions.QuickLogin => typeof(QuickLoginPage),
                StartPageOptions.Profile => typeof(ProfilePage),
                _ => typeof(ConfigPage),
            });
    }
}

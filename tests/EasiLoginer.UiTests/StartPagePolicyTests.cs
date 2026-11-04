using EasiLoginer.Presentation.Pages;
using EasiLoginer.Presentation.Shell;
using FluentAssertions;

namespace EasiLoginer.UiTests;

/// <summary>启动默认页：有档案进快捷登录，没有档案进档案页。</summary>
public sealed class StartPagePolicyTests
{
    /// <summary>有档案 → 快捷登录。</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void 有档案进快捷登录(int count)
    {
        StartPagePolicy.Resolve(count).Should().Be<QuickLoginPage>();
    }

    /// <summary>没有档案 → 档案页。</summary>
    [Fact]
    public void 无档案进档案页()
    {
        StartPagePolicy.Resolve(0).Should().Be<ProfilePage>();
    }
}

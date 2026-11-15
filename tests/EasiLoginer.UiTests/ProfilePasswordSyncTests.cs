using EasiLoginer.Presentation.Profiles;
using FluentAssertions;

namespace EasiLoginer.UiTests;

/// <summary>档案页密码框在导航时的清空判定。</summary>
public sealed class ProfilePasswordSyncTests
{
    /// <summary>无焦点时变空、且模型原本有密码：这是 WPF 卸载清理，不能写回模型。</summary>
    [Fact]
    public void 卸载清空不覆盖已有密码()
    {
        ProfilePasswordSync.IsProgrammaticClear(string.Empty, hasKeyboardFocus: false, currentValue: "pw123")
            .Should().BeTrue();
    }

    /// <summary>用户主动删空时焦点仍在框内，必须允许把密码改为空。</summary>
    [Fact]
    public void 用户主动清空要写回()
    {
        ProfilePasswordSync.IsProgrammaticClear(string.Empty, hasKeyboardFocus: true, currentValue: "pw123")
            .Should().BeFalse();
    }

    /// <summary>模型本来就是空密码时，清空没有任何副作用，也不需要特殊处理。</summary>
    [Fact]
    public void 原本为空时不算程序性清空()
    {
        ProfilePasswordSync.IsProgrammaticClear(string.Empty, hasKeyboardFocus: false, currentValue: string.Empty)
            .Should().BeFalse();
    }

    /// <summary>非空值无论在什么焦点状态都要正常写回。</summary>
    [Fact]
    public void 非空值正常写回()
    {
        ProfilePasswordSync.IsProgrammaticClear("new-pw", hasKeyboardFocus: false, currentValue: "old")
            .Should().BeFalse();
    }
}

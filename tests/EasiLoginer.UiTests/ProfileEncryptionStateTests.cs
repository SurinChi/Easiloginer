using EasiLoginer.Domain.Profiles;
using EasiLoginer.Presentation.Profiles;
using FluentAssertions;

namespace EasiLoginer.UiTests;

/// <summary>档案加密状态的按钮可用性与文案（文档 §5.2 状态机）。</summary>
public sealed class ProfileEncryptionStateTests
{
    /// <summary>明文档案：只能启用加密。</summary>
    [Fact]
    public void 明文状态()
    {
        var profile = new Profile { EncryptionEnabled = false };

        ProfileEncryptionState.CanEnable(profile).Should().BeTrue();
        ProfileEncryptionState.CanUnlock(profile, isUnlocked: false).Should().BeFalse();
        ProfileEncryptionState.CanDisable(profile, isUnlocked: false).Should().BeFalse();
        ProfileEncryptionState.Describe(profile, isUnlocked: false).Should().Contain("未加密");
    }

    /// <summary>已加密且锁定：可以解锁，但不能关闭加密（无密钥安全重写）。</summary>
    [Fact]
    public void 锁定状态()
    {
        var profile = new Profile { EncryptionEnabled = true };

        ProfileEncryptionState.CanEnable(profile).Should().BeFalse();
        ProfileEncryptionState.CanUnlock(profile, isUnlocked: false).Should().BeTrue();
        ProfileEncryptionState.CanDisable(profile, isUnlocked: false).Should().BeFalse();
        ProfileEncryptionState.Describe(profile, isUnlocked: false).Should().Contain("锁定");
    }

    /// <summary>已加密且已解锁：可以关闭加密，不再需要解锁。</summary>
    [Fact]
    public void 解锁状态()
    {
        var profile = new Profile { EncryptionEnabled = true };

        ProfileEncryptionState.CanEnable(profile).Should().BeFalse();
        ProfileEncryptionState.CanUnlock(profile, isUnlocked: true).Should().BeFalse();
        ProfileEncryptionState.CanDisable(profile, isUnlocked: true).Should().BeTrue();
        ProfileEncryptionState.Describe(profile, isUnlocked: true).Should().Contain("已解锁");
    }
}

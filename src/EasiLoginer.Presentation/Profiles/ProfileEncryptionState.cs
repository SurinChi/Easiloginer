using EasiLoginer.Domain.Profiles;

namespace EasiLoginer.Presentation.Profiles;

/// <summary>
/// 档案加密状态的纯判断，供界面决定按钮可用性与状态文案。
/// </summary>
/// <remarks>抽成纯函数便于测试；对应文档 §5.2 的状态机（Plain / Locked / Unlocked）。</remarks>
public static class ProfileEncryptionState
{
    /// <summary>是否可以启用加密（当前未加密）。</summary>
    public static bool CanEnable(Profile profile) => !profile.EncryptionEnabled;

    /// <summary>是否可以解锁（已加密且当前会话未解锁）。</summary>
    public static bool CanUnlock(Profile profile, bool isUnlocked) => profile.EncryptionEnabled && !isUnlocked;

    /// <summary>是否可以关闭加密（已加密且已解锁，才能安全重写明文）。</summary>
    public static bool CanDisable(Profile profile, bool isUnlocked) => profile.EncryptionEnabled && isUnlocked;

    /// <summary>状态文案。</summary>
    public static string Describe(Profile profile, bool isUnlocked)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (!profile.EncryptionEnabled)
        {
            return "档案未加密（明文保存）";
        }

        return isUnlocked
            ? "档案已加密，当前已解锁"
            : "档案已加密，当前锁定";
    }
}

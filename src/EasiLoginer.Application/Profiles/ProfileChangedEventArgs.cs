namespace EasiLoginer.Application.Profiles;

/// <summary>档案变更原因，与上游 <c>ProfileChangeReason</c> 对应。</summary>
public enum ProfileChangeReason
{
    /// <summary>档案内容变化。</summary>
    ProfileChanged,

    /// <summary>档案被保存。</summary>
    AutomationSaved,

    /// <summary>档案被删除。</summary>
    AutomationDeleted,

    /// <summary>加密状态变化。</summary>
    EncryptionChanged,
}

/// <summary>档案变更事件参数。</summary>
public sealed class ProfileChangedEventArgs(ProfileChangeReason reason) : EventArgs
{
    /// <summary>变更原因。</summary>
    public ProfileChangeReason Reason { get; } = reason;
}

namespace EasiLoginer.Domain.Automation;

/// <summary>一次登录的最终结果，与统计计数和 UI 状态一一对应。</summary>
public enum LoginOutcome
{
    /// <summary>登录成功。</summary>
    Success,

    /// <summary>登录失败（含重试耗尽）。</summary>
    Failed,

    /// <summary>用户主动停止或被跳过。</summary>
    Cancelled,
}

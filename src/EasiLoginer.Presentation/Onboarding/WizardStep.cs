namespace EasiLoginer.Presentation.Onboarding;

/// <summary>向导步骤的呈现类型（决定内容区渲染哪种控件）。</summary>
public enum WizardStepKind
{
    /// <summary>纯说明。</summary>
    Info,

    /// <summary>遥测开关。</summary>
    Telemetry,

    /// <summary>主题选择。</summary>
    Theme,

    /// <summary>登录方式选择。</summary>
    LoginMethod,

    /// <summary>希沃白板检测说明。</summary>
    EasiNote,

    /// <summary>快捷方式说明。</summary>
    Shortcuts,

    /// <summary>完成页：选择进入哪个页面。</summary>
    Finish,
}

/// <summary>一个向导步骤。</summary>
/// <param name="Title">标题。</param>
/// <param name="Description">说明文字。</param>
/// <param name="Kind">呈现类型。</param>
/// <param name="Skippable">是否允许"跳过此步"（不影响整体跳过）。</param>
public sealed record WizardStep(string Title, string Description, WizardStepKind Kind, bool Skippable = true);

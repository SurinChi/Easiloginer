namespace EasiLoginer.Presentation.Onboarding;

/// <summary>
/// 首次向导的步骤定义，对应文档 §10.7 的 7 步。
/// </summary>
/// <remarks>
/// 步骤只描述「显示什么」，具体取值由 <see cref="WizardViewModel"/> 持有，
/// 完成时一次性写回配置，避免中途半写。
/// </remarks>
public static class OobeSteps
{
    /// <summary>构建 7 个步骤。</summary>
    public static IReadOnlyList<WizardStep> Create() =>
    [
        new WizardStep(
            "欢迎使用 EasiLoginer",
            "EasiLoginer 用于手动触发希沃白板自动登录。登录只由你在应用内、命令行或快捷方式发起，不存在定时或外部调度。\n\n" +
            "本项目基于 EasiAuto（作者 hxabcd）重写，继续以 GPLv3 开源；不支持 ClassIsland。",
            WizardStepKind.Info,
            Skippable: false),

        new WizardStep(
            "外观",
            "选择应用主题。选择「跟随系统」时会读取 Windows 的浅色/深色设置。",
            WizardStepKind.Theme),

        new WizardStep(
            "登录方式",
            "固定位置：按坐标点击，开箱即用。自动定位：用 UI Automation 找控件，最稳。\n\n" +
            "图像识别：仅在 1920×1080 100% 或 4K 200% 环境下可用。令牌投递：需要先修补希沃白板。",
            WizardStepKind.LoginMethod),

        new WizardStep(
            "希沃白板",
            "请确认希沃白板已安装，路径与修补状态可在配置页查看。\n\n" +
            "修补需要管理员权限，并依赖随包分发的管道桥文件，请稍后在配置页手动完成。",
            WizardStepKind.EasiNote),

        new WizardStep(
            "系统集成",
            "首版不会自动创建桌面或开始菜单快捷方式，也不会写入开机启动项。\n\n" +
            "如需快捷启动，可自行创建指向 EasiLoginer.exe login 的快捷方式。",
            WizardStepKind.Shortcuts),

        new WizardStep(
            "完成",
            "设置已完成，可以开始使用了。",
            WizardStepKind.Finish),
    ];
}

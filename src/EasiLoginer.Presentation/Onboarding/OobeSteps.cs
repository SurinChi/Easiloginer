namespace EasiLoginer.Presentation.Onboarding;

/// <summary>
/// 首次向导的步骤定义，对应文档 §10.7（已去掉遥测步骤，共 6 步）。
/// </summary>
/// <remarks>
/// 步骤只描述「显示什么」，具体取值由 <see cref="WizardViewModel"/> 持有，
/// 完成时一次性写回配置，避免中途半写。
/// </remarks>
public static class OobeSteps
{
    /// <summary>用户协议正文（向导第一页展示，需勾选同意）。</summary>
    public const string AgreementText =
        "EasiLoginer 用户协议（摘要）\n\n" +
        "1. 本软件以 GNU General Public License v3.0 发布，按「现状」提供，不附带任何明示或默示担保。\n\n" +
        "2. EasiLoginer 仅在你本人操作时触发希沃白板登录；不会按时间、文件或进程事件自动触发。\n\n" +
        "3. 账号与密码只保存在本机 data 目录；启用主密码后以 ea$ 密文落盘。除你主动发起的检查更新外，" +
        "程序不联网，也不做任何遥测上报。\n\n" +
        "4. 修补希沃白板会修改其安装目录中的程序集，需要管理员权限，可能影响希沃白板的正常使用；" +
        "你可以随时在配置页撤销修补。\n\n" +
        "5. 修改、分发本软件须遵守 GPLv3，并保留上游 EasiAuto（作者 hxabcd）的版权与来源声明。\n\n" +
        "6. 使用本软件产生的任何后果由使用者自行承担。\n\n" +
        "勾选下方选项表示你已阅读并同意以上条款。";

    /// <summary>构建 6 个步骤。</summary>
    public static IReadOnlyList<WizardStep> Create() =>
    [
        new WizardStep(
            "用户协议",
            "首次使用前请阅读以下条款。",
            WizardStepKind.Agreement,
            Skippable: false),

        new WizardStep(
            "外观",
            "选择应用主题。选择「跟随系统」时会读取 Windows 的浅色/深色设置。",
            WizardStepKind.Theme),

        new WizardStep(
            "登录方式",
            "四种方式都可以在这里先选一个，之后随时能在配置页切换：\n\n" +
            "固定位置：按坐标点击，开箱即用，页面布局变化时可能失效。\n" +
            "图像识别：模板匹配，仅支持 1920×1080 100% 与 3840×2160 200%。\n" +
            "UI Automation：按控件识别，最稳，推荐首选。\n" +
            "令牌投递：速度最快，但需要先以管理员权限修补希沃白板。",
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

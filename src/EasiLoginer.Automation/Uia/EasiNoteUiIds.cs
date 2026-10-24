namespace EasiLoginer.Automation.Uia;

/// <summary>
/// 希沃白板登录界面的 <c>AutomationId</c> 常量。
/// </summary>
/// <remarks>
/// 这些值是 <b>2026-10 在真机 5.2.4.11451 上导出 UIA 元素树后确认的</b>，不是照抄文本推测：
/// <list type="bullet">
/// <item>窗口 <c>TheLoginWindow</c>（name「希沃白板」）</item>
/// <item>账号输入是 <c>UserIdTextBox</c>（ComboBox），其编辑框子节点为 <c>PART_EditableTextBox</c></item>
/// <item>PC 界面二维码容器是 <c>QrCodeControl</c>；文档里的 <c>IwbqrCodeControl</c> 属于 IWB 界面</item>
/// </list>
/// 上游文档只保证语义，不保证名字；这里以真机为准并保留两套候选。
/// </remarks>
public static class EasiNoteUiIds
{
    /// <summary>
/// 登录窗口（PC / IWB 通用）。真机窗口 name 为「希沃白板」，class 为 Window。
/// </summary>
    public const string LoginWindow = "TheLoginWindow";

    /// <summary>IWB 登录窗口（点击头像后出现）。</summary>
    public const string IwbLoginWindow = "IWBLogin";

    /// <summary>IWB 界面的头像按钮。</summary>
    public const string IwbProfileButton = "ProfileButton";

    /// <summary>IWB 界面的账号页切换。</summary>
    public const string IwbAccountRadio = "AccountRadioButton";

    /// <summary>PC 界面的账号页切换。</summary>
    public const string AccountLoginRadio = "AccountLoginRadioButton";

    /// <summary>账号输入（ComboBox）。</summary>
    public const string AccountInput = "UserIdTextBox";

    /// <summary>账号输入的可编辑子元素。</summary>
    public const string AccountEditPart = "PART_EditableTextBox";

    /// <summary>密码输入。</summary>
    public const string PasswordBox = "PasswordBox";

    /// <summary>同意协议复选框。</summary>
    public const string AgreementCheckBox = "AgreementCheckBox";

    /// <summary>登录按钮。</summary>
    public const string LoginButton = "LoginButton";

    /// <summary>PC 界面二维码容器（取矩形做遮罩）。</summary>
    public const string QrCodeControl = "QrCodeControl";

    /// <summary>IWB 界面二维码容器。</summary>
    public const string IwbQrCodeControl = "IwbqrCodeControl";
}

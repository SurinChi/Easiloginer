using System.Text.Json.Serialization;
using EasiLoginer.Domain.Geometry;

namespace EasiLoginer.Domain.Configuration;

/// <summary>希沃白板相关配置，对应上游 <c>EasiNoteConfig</c>。</summary>
public sealed class EasiNoteConfig
{
    /// <summary>自动检测安装路径；启用时忽略 <see cref="Path"/>。</summary>
    [JsonPropertyName("AutoPath")]
    public bool AutoPath { get; set; } = true;

    /// <summary>自定义启动器路径。</summary>
    [JsonPropertyName("Path")]
    public string Path { get; set; } = @"C:\Program Files (x86)\Seewo\EasiNote5\swenlauncher\swenlauncher.exe";

    /// <summary>进程名（不含 <c>.exe</c>）。</summary>
    [JsonPropertyName("ProcessName")]
    public string ProcessName { get; set; } = "EasiNote";

    /// <summary>窗口标题。</summary>
    [JsonPropertyName("WindowTitle")]
    public string WindowTitle { get; set; } = "希沃白板";

    /// <summary>启动参数；固定位置且非 IWB 模式时会被覆盖。</summary>
    [JsonPropertyName("Args")]
    public string Args { get; set; } = string.Empty;

    /// <summary>启动时一并结束的进程，英文逗号分隔。</summary>
    [JsonPropertyName("ExtraKills")]
    public string ExtraKills { get; set; } = string.Empty;
}

/// <summary>等待时长（秒），对应上游 <c>TimeoutConfig</c>。</summary>
public sealed class TimeoutConfig
{
    /// <summary>终止进程后的等待时间，范围 0–60。</summary>
    [JsonPropertyName("Terminate")]
    public double Terminate { get; set; } = 1;

    /// <summary>等待启动完成的最大时间，范围 0–60。</summary>
    [JsonPropertyName("LaunchPollingTimeout")]
    public double LaunchPollingTimeout { get; set; } = 30;

    /// <summary>轮询间隔，范围 0.1–60。</summary>
    [JsonPropertyName("LaunchPollingInterval")]
    public double LaunchPollingInterval { get; set; } = 0.5;

    /// <summary>启动后等待界面加载的时间，范围 0–60。</summary>
    [JsonPropertyName("AfterLaunch")]
    public double AfterLaunch { get; set; } = 2;

    /// <summary>点击「登录」后等待界面出现的时间，范围 0–60。</summary>
    [JsonPropertyName("EnterLoginUI")]
    public double EnterLoginUI { get; set; } = 3;

    /// <summary>切换账号登录标签页后的等待时间，范围 0–60。</summary>
    [JsonPropertyName("SwitchTab")]
    public double SwitchTab { get; set; } = 1;
}

/// <summary>固定位置登录的坐标，对应上游 <c>PositionConfig</c>。</summary>
/// <remarks>
/// 坐标必须基于 <see cref="BaseSize"/>；启用 <see cref="EnableScaling"/> 时按屏幕
/// 分辨率与缩放换算（见 <c>LoginWindowGeometry</c>）。
/// </remarks>
public sealed class PositionConfig
{
    /// <summary>启用智能缩放。</summary>
    [JsonPropertyName("EnableScaling")]
    public bool EnableScaling { get; set; } = true;

    /// <summary>「进入登录界面」按钮。</summary>
    [JsonPropertyName("EnterLogin")]
    public PixelPoint EnterLogin { get; set; } = new(172, 1044);

    /// <summary>「账号登录」标签页按钮。</summary>
    [JsonPropertyName("AccountLoginTab")]
    public PixelPoint AccountLoginTab { get; set; } = new(1090, 350);

    /// <summary>账号输入框。</summary>
    [JsonPropertyName("AccountInput")]
    public PixelPoint AccountInput { get; set; } = new(1000, 420);

    /// <summary>密码输入框。</summary>
    [JsonPropertyName("PasswordInput")]
    public PixelPoint PasswordInput { get; set; } = new(1000, 490);

    /// <summary>同意协议复选框。</summary>
    [JsonPropertyName("AgreementCheckbox")]
    public PixelPoint AgreementCheckbox { get; set; } = new(935, 724);

    /// <summary>登录按钮。</summary>
    [JsonPropertyName("LoginButton")]
    public PixelPoint LoginButton { get; set; } = new(1090, 560);

    /// <summary>基准分辨率（隐藏项，不显示在设置页）。</summary>
    [JsonPropertyName("BaseSize")]
    public PixelPoint BaseSize { get; set; } = new(1920, 1080);

    /// <summary>登录界面窗口大小（隐藏项）。</summary>
    [JsonPropertyName("LoginWindowSize")]
    public PixelPoint LoginWindowSize { get; set; } = new(808, 582);
}

/// <summary>登录相关配置，对应上游 <c>LoginConfig</c>。</summary>
public sealed class LoginConfig
{
    /// <summary>登录方式。</summary>
    [JsonPropertyName("Method")]
    public LoginMethod Method { get; set; } = LoginMethod.Fixed;

    /// <summary>跳过一次登录（公开课等临时禁用场景）。</summary>
    [JsonPropertyName("SkipOnce")]
    public bool SkipOnce { get; set; }

    /// <summary>已登录同一账号时跳过。</summary>
    [JsonPropertyName("SkipIfLoggedIn")]
    public bool SkipIfLoggedIn { get; set; } = true;

    /// <summary>终止 EasiAgent 进程。</summary>
    [JsonPropertyName("KillAgent")]
    public bool KillAgent { get; set; }

    /// <summary>
    /// 强制兼容模式输入（剪贴板粘贴）。
    /// 旧配置中的键名是 <c>ForceEnableScaling</c>，读取时由迁移层改名。
    /// </summary>
    [JsonPropertyName("ForceCompatibilityMode")]
    public bool ForceCompatibilityMode { get; set; }

    /// <summary>希沃白板选项。</summary>
    [JsonPropertyName("EasiNote")]
    public EasiNoteConfig EasiNote { get; set; } = new();

    /// <summary>等待时长。</summary>
    [JsonPropertyName("Timeout")]
    public TimeoutConfig Timeout { get; set; } = new();

    /// <summary>固定位置坐标。</summary>
    [JsonPropertyName("Position")]
    public PositionConfig Position { get; set; } = new();
}

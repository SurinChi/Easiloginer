using System.Reflection;
using EasiLoginer.Domain.Configuration;
using EasiLoginer.Domain.Geometry;

namespace EasiLoginer.Presentation.Configuration;

/// <summary>
/// 配置项目录：反射 <see cref="Config"/> 生成设置卡片（文档 §10.4）。
/// </summary>
/// <remarks>
/// 只负责"有哪些项、用什么编辑器、范围多少"，不持有值；
/// 值由每张卡片的视图模型从当前配置读写。
/// 分组与隐藏项对齐上游 <c>PAGE_INDEX</c>，并去掉 ClassIsland 相关内容。
/// </remarks>
public static class ConfigCatalog
{
    private static readonly Dictionary<string, string> GroupTitles = new(StringComparer.Ordinal)
    {
        ["Login"] = "登录选项",
        ["Warning"] = "警告弹窗",
        ["Banner"] = "警示横幅",
        ["StatusOverlay"] = "状态浮窗",
        ["App"] = "应用设置",
        ["Experimental"] = "实验性选项",
        ["Profile"] = "档案设置",
        ["Update"] = "更新设置",
    };

    /// <summary>配置树中不显示在界面上的段。</summary>
    private static readonly HashSet<string> HiddenSections = new(StringComparer.Ordinal)
    {
        "Debug",
        "Internal",
        "Statistics",
    };

    /// <summary>隐藏的具体项（与上游 <c>hidden</c> 标记一致）。</summary>
    private static readonly HashSet<string> HiddenPaths = new(StringComparer.Ordinal)
    {
        "Login.Position.BaseSize",
        "Login.Position.LoginWindowSize",
        "Update.LastVersion",
        "App.TelemetryEnabled",
    };

    private static readonly Dictionary<string, string> Titles = new(StringComparer.Ordinal)
    {
        ["Login.Method"] = "登录方式",
        ["Login.SkipOnce"] = "跳过一次",
        ["Login.SkipIfLoggedIn"] = "已登录则跳过",
        ["Login.KillAgent"] = "终止 EasiAgent 服务",
        ["Login.ForceCompatibilityMode"] = "强制兼容模式输入",
        ["Login.EasiNote.AutoPath"] = "自动检测路径",
        ["Login.EasiNote.Path"] = "自定义路径",
        ["Login.EasiNote.ProcessName"] = "进程名",
        ["Login.EasiNote.WindowTitle"] = "窗口标题",
        ["Login.EasiNote.Args"] = "启动参数",
        ["Login.EasiNote.ExtraKills"] = "查杀进程列表",
        ["Login.Timeout.Terminate"] = "终止进程等待时间",
        ["Login.Timeout.LaunchPollingTimeout"] = "等待启动超时",
        ["Login.Timeout.LaunchPollingInterval"] = "等待启动轮询间隔",
        ["Login.Timeout.AfterLaunch"] = "启动后等待时间",
        ["Login.Timeout.EnterLoginUI"] = "进入登录界面等待",
        ["Login.Timeout.SwitchTab"] = "切换标签等待",
        ["Login.Position.EnableScaling"] = "启用智能缩放",
        ["Login.Position.EnterLogin"] = "进入登录界面按钮",
        ["Login.Position.AccountLoginTab"] = "账号登录标签页",
        ["Login.Position.AccountInput"] = "账号输入框",
        ["Login.Position.PasswordInput"] = "密码输入框",
        ["Login.Position.AgreementCheckbox"] = "同意协议复选框",
        ["Login.Position.LoginButton"] = "登录按钮",
        ["Warning.Enabled"] = "启用警告弹窗",
        ["Warning.Timeout"] = "超时时长（秒）",
        ["Warning.MaxDelays"] = "最大推迟次数",
        ["Warning.DelayTime"] = "推迟时长（秒）",
        ["Warning.ShowUserName"] = "显示用户名",
        ["Banner.Enabled"] = "启用警示横幅",
        ["Banner.Style.Text"] = "文本",
        ["Banner.Style.TextFont"] = "文字字体",
        ["Banner.Style.TextColor"] = "文字颜色",
        ["Banner.Style.FgColor"] = "前景颜色",
        ["Banner.Style.BgColor"] = "背景颜色",
        ["Banner.Style.Fps"] = "刷新率",
        ["Banner.Style.TextSpeed"] = "文字滚动距离",
        ["Banner.Style.YOffset"] = "垂直偏移",
        ["StatusOverlay.Enabled"] = "启用状态浮窗",
        ["App.MaxRetries"] = "最大重试次数",
        ["App.Theme"] = "应用主题",
        ["App.LogEnabled"] = "启用日志记录",
        ["App.ResetDataAfterDays"] = "未启动清理（天，0 关闭，最短 7）",
        ["App.TelemetryEnabled"] = "参与匿名使用情况分析",
        ["Experimental.PrivacyMask.Enabled"] = "启用隐私保护遮罩",
        ["Experimental.PrivacyMask.MaskLeftTop"] = "遮罩位置",
        ["Experimental.PrivacyMask.MaskSize"] = "遮罩大小",
        ["Profile.SkipVerify"] = "保存档案时跳过校验",
        ["Update.Mode"] = "更新模式",
        ["Update.CheckAfterLogin"] = "登录后检查更新",
        ["Update.TargetUpdateChannel"] = "更新通道",
        ["Update.TargetDownloadSource"] = "下载镜像源",
    };

    /// <summary>枚举项的显示文本（顺序与枚举成员一致）。</summary>
    private static readonly Dictionary<Type, string[]> EnumLabels = new()
    {
        [typeof(LoginMethod)] = ["固定位置", "图像识别", "自动定位", "令牌投递"],
        [typeof(ThemeOptions)] = ["跟随系统", "浅色", "深色"],
        [typeof(UpdateMode)] = ["从不自动更新", "检查并通知", "检查并下载", "检查并安装"],
        [typeof(UpdateChannel)] = ["稳定通道", "测试通道"],
        [typeof(DownloadSource)] = ["自动选择", "GitHub", "ghproxy", "ghfast"],
    };

    /// <summary>数值范围（与上游 pydantic 的 ge/le 一致）。</summary>
    private static readonly Dictionary<string, (double Min, double Max)> Ranges = new(StringComparer.Ordinal)
    {
        ["Login.Timeout.Terminate"] = (0, 60),
        ["Login.Timeout.LaunchPollingTimeout"] = (0, 60),
        ["Login.Timeout.LaunchPollingInterval"] = (0.1, 60),
        ["Login.Timeout.AfterLaunch"] = (0, 60),
        ["Login.Timeout.EnterLoginUI"] = (0, 60),
        ["Login.Timeout.SwitchTab"] = (0, 60),
        ["Warning.Timeout"] = (5, 600),
        ["Warning.MaxDelays"] = (0, 3),
        ["Warning.DelayTime"] = (5, 300),
        ["Banner.Style.Fps"] = (1, 120),
        ["Banner.Style.TextSpeed"] = (-8, 8),
        ["App.MaxRetries"] = (0, 5),
        ["App.ResetDataAfterDays"] = (0, 3650),
    };

    /// <summary>生成全部分组与设置项描述。</summary>
    public static IReadOnlyList<SettingDescriptor> Build()
    {
        var descriptors = new List<SettingDescriptor>();
        var root = typeof(Config);

        foreach (var property in root.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (HiddenSections.Contains(property.Name) ||
                !GroupTitles.ContainsKey(property.Name) ||
                !IsConfigType(property.PropertyType))
            {
                continue;
            }

            Walk(property.PropertyType, property.Name, property.Name, descriptors);
        }

        return descriptors;
    }

    /// <summary>取分组标题。</summary>
    public static string GetGroupTitle(string key) => GroupTitles.TryGetValue(key, out var title) ? title : key;

    private static void Walk(Type type, string prefix, string group, List<SettingDescriptor> descriptors)
    {
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.CanRead || !property.CanWrite)
            {
                continue;
            }

            var path = $"{prefix}.{property.Name}";

            if (IsConfigType(property.PropertyType))
            {
                Walk(property.PropertyType, path, group, descriptors);
                continue;
            }

            if (HiddenPaths.Contains(path))
            {
                continue;
            }

            var kind = ResolveKind(property.PropertyType, path);
            if (kind is null)
            {
                continue;
            }

            var title = Titles.TryGetValue(path, out var mapped) ? mapped : property.Name;
            var options = kind == SettingKind.Enum ? GetEnumLabels(property.PropertyType) : null;
            Ranges.TryGetValue(path, out var range);

            descriptors.Add(new SettingDescriptor(
                path,
                group,
                title,
                kind.Value,
                options,
                range.Min,
                range.Max));
        }
    }

    private static SettingKind? ResolveKind(Type type, string path)
    {
        if (type == typeof(bool))
        {
            return SettingKind.Boolean;
        }

        if (type == typeof(string))
        {
            return SettingKind.Text;
        }

        if (type == typeof(int) || type == typeof(double) || type == typeof(float))
        {
            return SettingKind.Number;
        }

        if (type.IsEnum)
        {
            return SettingKind.Enum;
        }

        if (type == typeof(PixelPoint))
        {
            return SettingKind.Point;
        }

        return type == typeof(ArgbColor) ? SettingKind.Color : null;
    }

    private static bool IsConfigType(Type type)
        => type.Namespace == typeof(Config).Namespace && type.IsClass && !type.IsAbstract;

    private static string[] GetEnumLabels(Type enumType)
        => EnumLabels.TryGetValue(enumType, out var labels)
            ? labels
            : Enum.GetNames(enumType);
}

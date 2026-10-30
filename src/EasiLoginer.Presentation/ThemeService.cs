using Microsoft.Win32;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using EasiLoginer.Domain.Configuration;

namespace EasiLoginer.Presentation;

/// <summary>
/// 主题应用：<c>Auto</c> 跟随系统（读 <c>AppsUseLightTheme</c>），否则显式指定。
/// </summary>
/// <remarks>Mica 仅在 Win11 生效；Win10 上 WPF-UI 会自动回退（文档 §10.1）。</remarks>
public static class ThemeService
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string LightThemeValue = "AppsUseLightTheme";

    /// <summary>最近一次解析出的主题（供独立窗口自绘背景时使用）。</summary>
    public static ApplicationTheme CurrentTheme { get; private set; } = ApplicationTheme.Light;

    /// <summary>按配置应用主题。</summary>
    public static void Apply(ThemeOptions theme)
    {
        var resolved = theme switch
        {
            ThemeOptions.Dark => ApplicationTheme.Dark,
            ThemeOptions.Light => ApplicationTheme.Light,
            _ => IsSystemLightTheme() ? ApplicationTheme.Light : ApplicationTheme.Dark,
        };

        // 背板效果（Mica）在部分虚拟化环境下会导致窗口整体透明，
        // 这里统一使用 None + 不透明主题背景，等真机验证后再按平台启用。
        CurrentTheme = resolved;
        ApplicationThemeManager.Apply(resolved, WindowBackdropType.None, true);
    }

    /// <summary>读取系统是否使用浅色主题；读取失败按浅色处理。</summary>
    public static bool IsSystemLightTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
            return key?.GetValue(LightThemeValue) is not int value || value != 0;
        }
        catch (Exception exception) when (exception is System.Security.SecurityException or UnauthorizedAccessException)
        {
            return true;
        }
    }
}

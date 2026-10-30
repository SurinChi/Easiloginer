namespace EasiLoginer.Installer;

/// <summary>安装选项。</summary>
/// <param name="TargetDirectory">安装目录。</param>
/// <param name="DesktopShortcut">创建桌面快捷方式。</param>
/// <param name="StartMenuShortcut">创建开始菜单快捷方式。</param>
/// <param name="LaunchAfterInstall">安装完成后启动。</param>
public sealed record InstallOptions(
    string TargetDirectory,
    bool DesktopShortcut,
    bool StartMenuShortcut,
    bool LaunchAfterInstall);

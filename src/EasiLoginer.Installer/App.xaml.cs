using System.IO;
using System.Windows;

namespace EasiLoginer.Installer;

/// <summary>安装器入口：默认打开向导，支持 <c>--silent</c> 静默安装与 <c>--uninstall</c> 卸载。</summary>
public partial class App : Application
{
    /// <inheritdoc />
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var args = e.Args ?? [];

        if (HasFlag(args, "--uninstall", "/uninstall"))
        {
            RunUninstall();
            return;
        }

        if (HasFlag(args, "--silent", "/silent", "/s"))
        {
            RunSilentInstall();
            return;
        }

        new InstallerWindow().Show();
    }

    private static bool HasFlag(string[] args, params string[] flags)
        => args.Any(argument => flags.Contains(argument, StringComparer.OrdinalIgnoreCase));

    private void RunSilentInstall()
    {
        try
        {
            var options = new InstallOptions(
                InstallService.DefaultDirectory,
                DesktopShortcut: true,
                StartMenuShortcut: true,
                LaunchAfterInstall: false);

            InstallService.Install(options);
            Shutdown(0);
        }
        catch (Exception exception)
        {
            MessageBox.Show($"静默安装失败：{exception.Message}", "EasiLoginer 安装向导", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private void RunUninstall()
    {
        try
        {
            InstallService.Uninstall(AppContext.BaseDirectory);
            MessageBox.Show(
                "已卸载 EasiLoginer，安装目录与其中的数据将被删除。",
                "EasiLoginer 安装向导",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Shutdown(0);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show($"卸载失败：{exception.Message}", "EasiLoginer 安装向导", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }
}

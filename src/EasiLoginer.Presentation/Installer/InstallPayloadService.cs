using System.IO;
using Microsoft.Win32;

namespace EasiLoginer.Presentation.Installer;

/// <summary>安装选项。</summary>
public sealed record InstallOptions(
    string TargetDirectory,
    bool DesktopShortcut,
    bool StartMenuShortcut,
    bool LaunchAfterInstall);

/// <summary>安装进度。</summary>
public sealed record InstallProgress(int Completed, int Total, string CurrentFile);

/// <summary>
/// 安装向导的服务部分：把解压出来的程序复制到目标目录、建快捷方式、写卸载项。
/// </summary>
/// <remarks>
/// 由 SFX 解压到临时目录后以 <c>EasiLoginer.exe --install</c> 启动，
/// 因此源目录就是 <see cref="AppContext.BaseDirectory"/>。
/// </remarks>
public static class InstallPayloadService
{
    /// <summary>主程序文件名。</summary>
    public const string AppExecutable = "EasiLoginer.exe";

    /// <summary>默认安装目录（当前用户，无需管理员权限）。</summary>
    public static string DefaultTarget { get; } =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs",
            "EasiLoginer");

    /// <summary>校验目标目录：拒绝盘符根目录与 Windows 目录。</summary>
    public static void ValidateTarget(string target)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            throw new ArgumentException("安装目录不能为空。", nameof(target));
        }

        var full = Path.GetFullPath(target);
        var root = Path.GetPathRoot(full) ?? string.Empty;
        if (string.Equals(
                full.TrimEnd(Path.DirectorySeparatorChar),
                root.TrimEnd(Path.DirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("不能安装到盘符根目录。", nameof(target));
        }

        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (full.StartsWith(windows, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("不能安装到 Windows 目录。", nameof(target));
        }
    }

    /// <summary>执行安装（源目录为当前程序所在目录）。</summary>
    public static string Install(
        InstallOptions options,
        IProgress<InstallProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var source = Path.GetFullPath(AppContext.BaseDirectory);
        var target = Path.GetFullPath(options.TargetDirectory);
        ValidateTarget(target);

        var files = Directory
            .EnumerateFiles(source, "*", SearchOption.AllDirectories)
            .ToArray();

        Directory.CreateDirectory(target);

        for (var index = 0; index < files.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var relative = Path.GetRelativePath(source, files[index]);
            var destination = Path.Combine(target, relative);

            var destinationDirectory = Path.GetDirectoryName(destination);
            if (!string.IsNullOrEmpty(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            // 正在运行的卸载脚本/自身文件跳过，避免锁定失败（目标目录已有则覆盖）
            if (string.Equals(destination, Path.Combine(target, "Uninstall.cmd"), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            File.Copy(files[index], destination, overwrite: true);
            progress?.Report(new InstallProgress(index + 1, files.Length, relative));
        }

        CreateShortcuts(target, options);
        RegisterUninstall(target);

        return target;
    }

    /// <summary>创建桌面/开始菜单快捷方式。</summary>
    public static void CreateShortcuts(string target, InstallOptions options)
    {
        if (!options.DesktopShortcut && !options.StartMenuShortcut)
        {
            return;
        }

        var executable = Path.Combine(target, AppExecutable);
        if (!File.Exists(executable))
        {
            return;
        }

        var shellType = Type.GetTypeFromProgID("WScript.Shell");
        if (shellType is null)
        {
            return;
        }

        dynamic shell = Activator.CreateInstance(shellType)!;

        if (options.DesktopShortcut)
        {
            SaveShortcut(
                shell,
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "EasiLoginer.lnk"),
                executable,
                target);
        }

        if (options.StartMenuShortcut)
        {
            SaveShortcut(
                shell,
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "EasiLoginer.lnk"),
                executable,
                target);
        }
    }

    private static void SaveShortcut(dynamic shell, string linkPath, string executable, string workingDirectory)
    {
        dynamic shortcut = shell.CreateShortcut(linkPath);
        shortcut.TargetPath = executable;
        shortcut.WorkingDirectory = workingDirectory;
        shortcut.IconLocation = executable;
        shortcut.Description = "EasiLoginer";
        shortcut.Save();
    }

    /// <summary>写「应用和功能」卸载项并生成 Uninstall.cmd。</summary>
    public static void RegisterUninstall(string target)
    {
        var desktopLink = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "EasiLoginer.lnk");
        var startMenuLink = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Programs), "EasiLoginer.lnk");

        var uninstaller = Path.Combine(target, "Uninstall.cmd");
        var script = string.Join(
            "\r\n",
            "@echo off",
            "rem EasiLoginer uninstaller generated by setup",
            "reg delete \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\EasiLoginer\" /f >nul 2>&1",
            $"del /q \"{desktopLink}\" >nul 2>&1",
            $"del /q \"{startMenuLink}\" >nul 2>&1",
            "start \"\" /min cmd /c \"timeout /t 2 >nul & rd /s /q \"\"%~dp0\"\"\"",
            "exit /b 0",
            string.Empty);

        File.WriteAllText(uninstaller, script, System.Text.Encoding.ASCII);

        using var key = Registry.CurrentUser.CreateSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Uninstall\EasiLoginer");
        if (key is null)
        {
            return;
        }

        var version = typeof(InstallPayloadService).Assembly.GetName().Version?.ToString(3) ?? "0.1.1";
        key.SetValue("DisplayName", "EasiLoginer");
        key.SetValue("DisplayVersion", version);
        key.SetValue("Publisher", "SurinChi");
        key.SetValue("InstallLocation", target);
        key.SetValue("DisplayIcon", Path.Combine(target, AppExecutable));
        key.SetValue("UninstallString", $"\"{uninstaller}\"");
        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
    }

    /// <summary>从目标目录启动程序。</summary>
    public static void Launch(string target)
    {
        var executable = Path.Combine(target, AppExecutable);
        if (!File.Exists(executable))
        {
            return;
        }

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(executable)
        {
            WorkingDirectory = target,
            UseShellExecute = true,
        });
    }

    /// <summary>清理临时解压目录（若源目录不是目标目录）。</summary>
    public static void CleanupSource(string target)
    {
        var source = Path.GetFullPath(AppContext.BaseDirectory).TrimEnd(Path.DirectorySeparatorChar);
        var destination = Path.GetFullPath(target).TrimEnd(Path.DirectorySeparatorChar);

        if (string.Equals(source, destination, StringComparison.OrdinalIgnoreCase) ||
            !source.StartsWith(Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe")
        {
            Arguments = $"/c ping -n 3 127.0.0.1 >nul & rd /s /q \"{source}\"",
            CreateNoWindow = true,
            UseShellExecute = false,
        });
    }
}

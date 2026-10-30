using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using Microsoft.Win32;

namespace EasiLoginer.Installer;

/// <summary>
/// 安装/卸载逻辑：解压内置负载、创建快捷方式、写卸载信息。
/// </summary>
/// <remarks>
/// 负载来自嵌入资源 <c>EasiLoginer.payload.zip</c>（由发布脚本注入），
/// 因此安装包是单个自解压 EXE，不需要用户先装 .NET。
/// </remarks>
public static class InstallService
{
    /// <summary>负载资源名。</summary>
    public const string PayloadResourceName = "EasiLoginer.payload.zip";

    /// <summary>主程序文件名。</summary>
    public const string AppExecutable = "EasiLoginer.exe";

    /// <summary>默认安装目录：<c>%LOCALAPPDATA%\Programs\EasiLoginer</c>（无需管理员权限）。</summary>
    public static string DefaultDirectory { get; } =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs",
            "EasiLoginer");

    /// <summary>读取内嵌负载；未注入时返回 <see langword="null"/>。</summary>
    public static Stream? OpenPayload()
        => Assembly.GetExecutingAssembly().GetManifestResourceStream(PayloadResourceName);

    /// <summary>执行安装。</summary>
    /// <param name="options">安装选项。</param>
    /// <param name="progress">进度回调（已完成数、总数、当前文件）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public static void Install(
        InstallOptions options,
        IProgress<InstallProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var target = Path.GetFullPath(options.TargetDirectory);
        ValidateTarget(target);

        using var payload = OpenPayload()
            ?? throw new InvalidOperationException("安装包缺少内置负载（未通过发布脚本打包）。");

        using var archive = new ZipArchive(payload, ZipArchiveMode.Read);
        var entries = archive.Entries.Where(entry => entry.FullName.Length > 0).ToArray();

        Directory.CreateDirectory(target);

        // 负载 ZIP 顶层是 EasiLoginer/（与绿色版结构一致），安装时把这层去掉，
        // 否则会装成 安装目录\EasiLoginer\EasiLoginer.exe。
        var prefix = ComputeRootPrefix(entries);

        for (var index = 0; index < entries.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entry = entries[index];

            var name = prefix.Length > 0 && entry.FullName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                ? entry.FullName[prefix.Length..]
                : entry.FullName;

            if (name.Length == 0)
            {
                progress?.Report(new InstallProgress(index + 1, entries.Length, entry.FullName));
                continue;
            }

            // ZIP 内路径统一使用 '/'；拒绝跳出安装目录的条目
            var relative = name.Replace('/', Path.DirectorySeparatorChar);
            var destination = Path.GetFullPath(Path.Combine(target, relative));
            if (!destination.StartsWith(target, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (name.EndsWith('/'))
            {
                Directory.CreateDirectory(destination);
                progress?.Report(new InstallProgress(index + 1, entries.Length, entry.FullName));
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            entry.ExtractToFile(destination, overwrite: true);
            progress?.Report(new InstallProgress(index + 1, entries.Length, entry.FullName));
        }

        CreateShortcuts(target, options);
        RegisterUninstall(target);
    }

    /// <summary>求所有条目共同的顶层目录（例如 <c>EasiLoginer/</c>）；没有共同前缀时返回空串。</summary>
    private static string ComputeRootPrefix(IReadOnlyList<ZipArchiveEntry> entries)
    {
        if (entries.Count == 0)
        {
            return string.Empty;
        }

        var first = entries[0].FullName;
        var slash = first.IndexOf('/');
        if (slash <= 0)
        {
            return string.Empty;
        }

        var candidate = first[..(slash + 1)];
        return entries.All(entry => entry.FullName.StartsWith(candidate, StringComparison.OrdinalIgnoreCase))
            ? candidate
            : string.Empty;
    }

    /// <summary>创建快捷方式（桌面 / 开始菜单）并复制卸载程序。</summary>
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
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            SaveShortcut(shell, Path.Combine(desktop, "EasiLoginer.lnk"), executable, target);
        }

        if (options.StartMenuShortcut)
        {
            var programs = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
            SaveShortcut(shell, Path.Combine(programs, "EasiLoginer.lnk"), executable, target);
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

    /// <summary>写入「应用和功能」里的卸载项，并在安装目录生成 <c>Uninstall.cmd</c>。</summary>
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

        key.SetValue("DisplayName", "EasiLoginer");
        key.SetValue("DisplayVersion", typeof(InstallService).Assembly.GetName().Version?.ToString(3) ?? "0.0.4");
        key.SetValue("Publisher", "SurinChi");
        key.SetValue("InstallLocation", target);
        key.SetValue("DisplayIcon", Path.Combine(target, AppExecutable));
        key.SetValue("UninstallString", $"\"{uninstaller}\"");
        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
    }

    /// <summary>卸载：删除快捷方式、注册表项与安装目录（数据一并删除）。</summary>
    public static void Uninstall(string installDirectory)
    {
        var target = Path.GetFullPath(installDirectory);

        DeleteShortcut(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "EasiLoginer.lnk"));
        DeleteShortcut(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Programs), "EasiLoginer.lnk"));

        Registry.CurrentUser.DeleteSubKeyTree(
            @"Software\Microsoft\Windows\CurrentVersion\Uninstall\EasiLoginer",
            throwOnMissingSubKey: false);

        // 运行中的卸载程序无法删除自己：交给 cmd 延迟删除整个目录
        var script = $"ping -n 3 127.0.0.1 >nul & rd /s /q \"{target}\"";
        Process.Start(new ProcessStartInfo("cmd.exe", $"/c {script}")
        {
            CreateNoWindow = true,
            UseShellExecute = false,
        });
    }

    private static void DeleteShortcut(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // 快捷方式删不掉不影响卸载主体
        }
    }

    /// <summary>校验目标目录：拒绝盘符根目录与系统目录。</summary>
    public static void ValidateTarget(string target)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            throw new ArgumentException("安装目录不能为空。", nameof(target));
        }

        var full = Path.GetFullPath(target);
        var root = Path.GetPathRoot(full) ?? string.Empty;
        if (string.Equals(full.TrimEnd(Path.DirectorySeparatorChar), root.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("不能安装到盘符根目录。", nameof(target));
        }

        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (full.StartsWith(windows, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("不能安装到 Windows 目录。", nameof(target));
        }
    }
}

/// <summary>安装进度。</summary>
/// <param name="Completed">已解压条目数。</param>
/// <param name="Total">条目总数。</param>
/// <param name="CurrentFile">当前文件。</param>
public sealed record InstallProgress(int Completed, int Total, string CurrentFile);

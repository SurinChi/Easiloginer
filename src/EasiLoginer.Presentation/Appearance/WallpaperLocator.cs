using System.IO;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace EasiLoginer.Presentation.Appearance;

/// <summary>
/// 桌面壁纸定位与图片加载。
/// </summary>
/// <remarks>
/// Windows 的壁纸可能是普通图片、幻灯片缓存的 <c>TranscodedWallpaper</c>（无扩展名），
/// 或 <c>CachedFiles</c> 中的某一张。这里按可靠性从高到低依次尝试，全部失败时
/// 返回 <see langword="null"/>，由调用方回退到主题纯色背景。
/// </remarks>
public static class WallpaperLocator
{
    private static readonly string[] SupportedExtensions = [".png", ".jpg", ".jpeg", ".bmp", ".webp", ".gif"];

    /// <summary>定位当前桌面壁纸文件；找不到时返回 <see langword="null"/>。</summary>
    public static string? GetDesktopWallpaperPath()
    {
        var fromRegistry = ReadRegistryWallpaper();
        if (IsReadableImage(fromRegistry))
        {
            return fromRegistry;
        }

        var themes = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft",
            "Windows",
            "Themes");

        var transcoded = Path.Combine(themes, "TranscodedWallpaper");
        if (File.Exists(transcoded))
        {
            return transcoded;
        }

        var cached = Path.Combine(themes, "CachedFiles");
        if (!Directory.Exists(cached))
        {
            return null;
        }

        try
        {
            return Directory
                .EnumerateFiles(cached)
                .Where(IsReadableImage)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>读取并解码当前桌面壁纸；失败时返回 <see langword="null"/>。</summary>
    public static BitmapSource? LoadDesktopWallpaper() => LoadImage(GetDesktopWallpaperPath());

    /// <summary>读取并解码指定图片；路径为空、文件不存在或格式不支持时返回 <see langword="null"/>。</summary>
    public static BitmapSource? LoadImage(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        string fullPath;
        try
        {
            fullPath = Environment.ExpandEnvironmentVariables(path.Trim().Trim('"'));
            if (!File.Exists(fullPath))
            {
                return null;
            }
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }

        try
        {
            using var stream = File.OpenRead(fullPath);

            // OnLoad + IgnoreImageCache：解码后立即释放文件句柄，避免用户换壁纸时被本程序锁住。
            var frame = BitmapFrame.Create(stream, BitmapCreateOptions.IgnoreImageCache, BitmapCacheOption.OnLoad);
            frame.Freeze();
            return frame;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException
                                              or ArgumentException or InvalidOperationException or FileFormatException)
        {
            return null;
        }
    }

    private static string? ReadRegistryWallpaper()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop");
            return key?.GetValue("WallPaper") as string;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
                                              or System.Security.SecurityException)
        {
            return null;
        }
    }

    private static bool IsReadableImage(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return false;
        }

        var extension = Path.GetExtension(path);
        return extension.Length == 0 ||
               SupportedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }
}

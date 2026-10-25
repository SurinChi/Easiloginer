using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using EasiLoginer.Domain.Geometry;

namespace EasiLoginer.Automation.Cv;

/// <summary>
/// 真机实现：截取主屏 → 加载模板 PNG → 逐像素匹配。
/// </summary>
/// <remarks>
/// 进程是 PerMonitorV2 感知的，因此截图与模板都按物理像素处理，不做额外缩放换算。
/// 模板目录默认为 <c>resources/EasiNoteUI</c>（随发布包分发）。
/// </remarks>
public sealed class ScreenTemplateLocator : ITemplateLocator
{
    private readonly string _templateDirectory;

    /// <summary>创建定位器。</summary>
    /// <param name="templateDirectory">模板目录；默认 <c>AppContext.BaseDirectory/resources/EasiNoteUI</c>。</param>
    public ScreenTemplateLocator(string? templateDirectory = null)
    {
        _templateDirectory = templateDirectory
                             ?? Path.Combine(AppContext.BaseDirectory, "resources", "EasiNoteUI");
    }

    /// <inheritdoc />
    public PixelPoint? Locate(string templateName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templateName);

        var path = Path.Combine(_templateDirectory, templateName + ".png");
        if (!File.Exists(path))
        {
            return null;
        }

        using var template = new Bitmap(path);
        using var screen = CaptureScreen();

        return TemplateMatcher.Find(screen, template);
    }

    private static Bitmap CaptureScreen()
    {
        var width = GetSystemMetrics(SystemMetricWidth);
        var height = GetSystemMetrics(SystemMetricHeight);

        var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.CopyFromScreen(0, 0, 0, 0, new Size(width, height), CopyPixelOperation.SourceCopy);

        return bitmap;
    }

    private const int SystemMetricWidth = 0;
    private const int SystemMetricHeight = 1;

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);
}

using EasiLoginer.Domain.Geometry;

namespace EasiLoginer.Automation.Cv;

/// <summary>
/// 模板图变体选择，对应上游 <c>cv.py</c> 的 <c>_IMAGE_ENVIRONMENTS</c>。
/// </summary>
/// <remarks>
/// 模板只在两种显示环境下采集，其他分辨率/缩放无法保证逐像素匹配，
/// 因此必须在启动希沃之前就判定（失败不可重试）。
/// </remarks>
public static class ImageTemplateVariant
{
    private static readonly (int Width, int Height, double Scale, string Suffix)[] Environments =
    [
        (1920, 1080, 1.0, ""),
        (3840, 2160, 2.0, "_4k"),
    ];

    /// <summary>当前环境支持的变体后缀；不支持时返回 <see langword="null"/>。</summary>
    public static string? Resolve(PixelPoint physicalSize, double scale)
    {
        foreach (var (width, height, environmentScale, suffix) in Environments)
        {
            if (physicalSize.X == width && physicalSize.Y == height && Math.Abs(scale - environmentScale) < 0.01)
            {
                return suffix;
            }
        }

        return null;
    }

    /// <summary>面向用户的“支持哪些环境”文案。</summary>
    public static string SupportedDescription
        => string.Join("、", Environments.Select(item => $"{item.Width}x{item.Height} {Math.Round(item.Scale * 100)}%"));

    /// <summary>当前环境（用于错误提示）。</summary>
    public static string Describe(PixelPoint physicalSize, double scale)
        => $"{physicalSize.X}x{physicalSize.Y} {Math.Round(scale * 100)}%";
}

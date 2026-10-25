using System.Drawing;
using System.Drawing.Imaging;
using EasiLoginer.Domain.Geometry;

namespace EasiLoginer.Automation.Cv;

/// <summary>
/// 逐像素模板匹配（与上游 pyscreeze 的“精确匹配”语义一致，不引入 OpenCV）。
/// </summary>
/// <remarks>
/// 先用少量像素做预筛，再整行比较，避免在 1080p/4K 全屏上做无谓的全量比较。
/// 返回值是匹配区域的<b>中心点</b>（物理像素），与 pyautogui 的 <c>locateCenterOnScreen</c> 对应。
/// </remarks>
public static class TemplateMatcher
{
    /// <summary>在 <paramref name="screen"/> 中查找 <paramref name="template"/>；找不到返回 <see langword="null"/>。</summary>
    public static PixelPoint? Find(Bitmap screen, Bitmap template)
    {
        ArgumentNullException.ThrowIfNull(screen);
        ArgumentNullException.ThrowIfNull(template);

        if (template.Width > screen.Width || template.Height > screen.Height)
        {
            return null;
        }

        var haystack = ToPixels(screen, out var hayWidth, out var hayHeight);
        var needle = ToPixels(template, out var templateWidth, out var templateHeight);

        return Find(haystack, hayWidth, hayHeight, needle, templateWidth, templateHeight);
    }

    /// <summary>纯数组版本，便于单测。</summary>
    public static PixelPoint? Find(
        int[] haystack,
        int haystackWidth,
        int haystackHeight,
        int[] needle,
        int needleWidth,
        int needleHeight)
    {
        ArgumentNullException.ThrowIfNull(haystack);
        ArgumentNullException.ThrowIfNull(needle);

        if (needleWidth <= 0 || needleHeight <= 0 ||
            needleWidth > haystackWidth || needleHeight > haystackHeight)
        {
            return null;
        }

        var first = needle[0];
        var last = needle[needleWidth - 1];

        for (var y = 0; y <= haystackHeight - needleHeight; y++)
        {
            var rowStart = y * haystackWidth;

            for (var x = 0; x <= haystackWidth - needleWidth; x++)
            {
                var index = rowStart + x;

                // 两个角像素预筛，绝大多数位置会被直接排除
                if (haystack[index] != first || haystack[index + needleWidth - 1] != last)
                {
                    continue;
                }

                if (RowMatches(haystack, haystackWidth, index, needle, needleWidth, needleHeight))
                {
                    return new PixelPoint(x + (needleWidth / 2), y + (needleHeight / 2));
                }
            }
        }

        return null;
    }

    private static bool RowMatches(
        int[] haystack,
        int haystackWidth,
        int startIndex,
        int[] needle,
        int needleWidth,
        int needleHeight)
    {
        for (var row = 0; row < needleHeight; row++)
        {
            var hay = haystack.AsSpan(startIndex + (row * haystackWidth), needleWidth);
            var target = needle.AsSpan(row * needleWidth, needleWidth);

            if (!hay.SequenceEqual(target))
            {
                return false;
            }
        }

        return true;
    }

    private static int[] ToPixels(Bitmap bitmap, out int width, out int height)
    {
        width = bitmap.Width;
        height = bitmap.Height;

        var pixels = new int[width * height];
        var data = bitmap.LockBits(
            new Rectangle(0, 0, width, height),
            ImageLockMode.ReadOnly,
            PixelFormat.Format32bppArgb);

        try
        {
            // 逐行拷贝，避免 unsafe；一行 4 字节/像素与 int[] 布局一致
            var rowBytes = new byte[width * 4];
            for (var y = 0; y < height; y++)
            {
                System.Runtime.InteropServices.Marshal.Copy(
                    IntPtr.Add(data.Scan0, y * data.Stride),
                    rowBytes,
                    0,
                    rowBytes.Length);

                Buffer.BlockCopy(rowBytes, 0, pixels, y * width * 4, rowBytes.Length);
            }
        }
        finally
        {
            bitmap.UnlockBits(data);
        }

        return pixels;
    }
}

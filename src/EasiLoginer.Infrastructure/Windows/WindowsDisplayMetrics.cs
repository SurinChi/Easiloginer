using System.Runtime.InteropServices;
using EasiLoginer.Application.Automation;
using EasiLoginer.Domain.Geometry;

namespace EasiLoginer.Infrastructure.Windows;

/// <summary>
/// 主屏度量：物理尺寸、缩放比例与刷新率。
/// </summary>
/// <remarks>
/// <para>与上游 <c>core/display.py</c> 的语义对齐：物理尺寸来自 DPI 感知后的系统度量，
/// 逻辑尺寸 = 物理尺寸 / 缩放（对应 Qt 的 <c>screen.geometry()</c>）。</para>
/// <para>刷新率通过 <c>EnumDisplaySettingsW</c> 读取并限制在 [30, 240]，失败回退 60。</para>
/// </remarks>
public sealed class WindowsDisplayMetrics : IDisplayMetrics
{
    private const int SmCxScreen = 0;
    private const int SmCyScreen = 1;
    private const int EnumCurrentSettings = -1;

    /// <inheritdoc />
    public ScreenMetrics Capture()
    {
        var scale = GetScale();
        var width = GetSystemMetrics(SmCxScreen);
        var height = GetSystemMetrics(SmCyScreen);

        if (width <= 0 || height <= 0 || scale <= 0)
        {
            return new ScreenMetrics(new PixelPoint(1920, 1080), 1.0, 60);
        }

        var logicalWidth = (int)Math.Round(width / scale, MidpointRounding.AwayFromZero);
        var logicalHeight = (int)Math.Round(height / scale, MidpointRounding.AwayFromZero);

        return new ScreenMetrics(new PixelPoint(logicalWidth, logicalHeight), scale, GetRefreshRate());
    }

    private static double GetScale()
    {
        try
        {
            var dpi = GetDpiForSystem();
            return dpi > 0 ? dpi / 96.0 : 1.0;
        }
        catch (EntryPointNotFoundException)
        {
            return 1.0;
        }
    }

    private static int GetRefreshRate()
    {
        try
        {
            var mode = new DevMode
            {
                Size = (ushort)Marshal.SizeOf<DevMode>(),
            };

            if (!EnumDisplaySettingsW(null, EnumCurrentSettings, ref mode))
            {
                return 60;
            }

            return mode.DisplayFrequency is >= 30 and <= 240 ? (int)mode.DisplayFrequency : 60;
        }
        catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException)
        {
            return 60;
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DevMode
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;

        public ushort SpecVersion;
        public ushort DriverVersion;
        public ushort Size;
        public ushort DriverExtra;
        public uint Fields;
        public int PositionX;
        public int PositionY;
        public uint DisplayOrientation;
        public uint DisplayFixedOutput;
        public short Color;
        public short Duplex;
        public short YResolution;
        public short TrueTypeOption;
        public short Collate;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string FormName;

        public ushort LogPixels;
        public uint BitsPerPel;
        public uint PelsWidth;
        public uint PelsHeight;
        public uint DisplayFlags;
        public uint DisplayFrequency;
    }

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForSystem();

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplaySettingsW(string? deviceName, int mode, ref DevMode devMode);
}

using System.Windows;
using System.Windows.Interop;
using EasiLoginer.Presentation.Login;

namespace EasiLoginer.Presentation.Overlays;

/// <summary>
/// 警示横幅窗口：置顶、无边框、点击穿透、不抢焦点，横跨主屏。
/// </summary>
public partial class BannerWindow : Window
{
    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x00000020;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;

    private readonly BannerCanvas _canvas;

    /// <summary>创建横幅窗口。</summary>
    public BannerWindow(BannerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        InitializeComponent();

        Left = 0;
        Top = options.Top;
        Width = SystemParameters.PrimaryScreenWidth;
        Height = Math.Max(40, options.Height);

        _canvas = new BannerCanvas(options);
        RootGrid.Children.Add(_canvas);

        SourceInitialized += (_, _) => ApplyClickThrough();
        Loaded += (_, _) => _canvas.Start();
        Closed += (_, _) => _canvas.Stop();
    }

    private void ApplyClickThrough()
    {
        var handle = new WindowInteropHelper(this).Handle;
        var style = NativeMethods.GetWindowLong(handle, GwlExStyle);

        _ = NativeMethods.SetWindowLong(
            handle,
            GwlExStyle,
            style | WsExTransparent | WsExToolWindow | WsExNoActivate);
    }
}

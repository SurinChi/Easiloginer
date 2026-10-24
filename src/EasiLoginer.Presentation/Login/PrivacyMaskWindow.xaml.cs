using System.Windows;
using System.Windows.Interop;

namespace EasiLoginer.Presentation.Login;

/// <summary>
/// 隐私遮罩：置顶、无边框、点击穿透、不抢焦点（文档 §10.5）。
/// </summary>
public partial class PrivacyMaskWindow : Window
{
    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x00000020;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;

    /// <summary>创建遮罩窗口。</summary>
    public PrivacyMaskWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => ApplyClickThrough();
    }

    /// <summary>按 DIP 坐标显示遮罩。</summary>
    public void ShowAt(double left, double top, double width, double height)
    {
        Left = left;
        Top = top;
        Width = Math.Max(1, width);
        Height = Math.Max(1, height);

        if (!IsVisible)
        {
            Show();
        }
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

using System.Windows.Media;

namespace EasiLoginer.Presentation.Onboarding;

/// <summary>
/// 向导窗口的背景色：不依赖主题资源字典，直接用解析后的主题给窗口和内容区上色。
/// </summary>
/// <remarks>
/// 背景：<c>FluentWindow</c> 是独立窗口，切换主题时它的 <c>Background</c> 绑定
/// 有时不刷新，出现"文字已经变白、底色还是白的"这种不可读状态。
/// 这里直接按当前主题设置纯色背景，保证与文字颜色配套。
/// </remarks>
internal static class WizardTheme
{
    private static readonly Brush Dark = new SolidColorBrush(Color.FromRgb(0x20, 0x20, 0x20));
    private static readonly Brush Light = new SolidColorBrush(Colors.White);

    /// <summary>按当前应用主题设置窗口与根容器的背景。</summary>
    public static void Apply(System.Windows.Window window, System.Windows.FrameworkElement root)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(root);

        var brush = ThemeService.CurrentTheme == Wpf.Ui.Appearance.ApplicationTheme.Dark ? Dark : Light;

        window.Background = brush;

        if (root is System.Windows.Controls.Panel panel)
        {
            panel.Background = brush;
        }
    }
}

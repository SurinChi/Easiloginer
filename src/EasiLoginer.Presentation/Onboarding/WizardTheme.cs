using System.Windows.Media;
using EasiLoginer.Presentation.Appearance;

namespace EasiLoginer.Presentation.Onboarding;

/// <summary>
/// 向导窗口的背景处理：窗口与根容器保持透明，让 XAML 中的背景层 / 亚克力层显示出来。
/// </summary>
/// <remarks>
/// 背景：<c>FluentWindow</c> 是独立窗口，切换主题时它的 <c>Background</c> 绑定
/// 有时不刷新，出现"文字已经变白、底色还是白的"这种不可读状态。
/// 现在底色由 <see cref="AppearanceService"/> 统一维护，窗口自身必须是透明的，
/// 否则会盖住背景层。
/// </remarks>
internal static class WizardTheme
{
    /// <summary>把窗口与根容器设为透明，交给背景层绘制。</summary>
    public static void Apply(System.Windows.Window window, System.Windows.FrameworkElement root)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(root);

        window.Background = Brushes.Transparent;

        if (root is System.Windows.Controls.Panel panel)
        {
            panel.Background = Brushes.Transparent;
        }
    }
}

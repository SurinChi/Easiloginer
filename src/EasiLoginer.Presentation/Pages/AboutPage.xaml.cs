using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using EasiLoginer.Presentation.Legal;

namespace EasiLoginer.Presentation.Pages;

/// <summary>关于页：应用横幅、作者信息、核心亮点与许可入口。</summary>
public partial class AboutPage : Page
{
    /// <summary>创建关于页。</summary>
    public AboutPage(AppServices services)
    {
        ArgumentNullException.ThrowIfNull(services);

        InitializeComponent();
        PageLayout.ConstrainToNavigationHost(this);
        VersionText.Text = $"v{AppVersion.Text} · GPLv3";
    }

    /// <summary>
    /// 横幅按 1600×560 的原始比例跟随内容宽度缩放，避免窄窗口下被裁掉左右两侧。
    /// </summary>
    private void OnBannerSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender is not Border border || e.NewSize.Width <= 0)
        {
            return;
        }

        var height = Math.Round(e.NewSize.Width * 560.0 / 1600.0);
        if (Math.Abs(border.Height - height) > 0.5)
        {
            border.Height = height;
        }
    }

    private void OnAgreementClicked(object sender, RoutedEventArgs e)
    {
        var window = new UserAgreementWindow { Owner = Window.GetWindow(this) };
        Wpf.Ui.Appearance.ApplicationThemeManager.Apply(window);
        window.ShowDialog();
    }

    private void OnLinkNavigate(object sender, RequestNavigateEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
        {
            // 没有默认浏览器时忽略：外链不是关于页的核心功能
        }

        e.Handled = true;
    }
}

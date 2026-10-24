using System.Windows;
using System.Windows.Threading;
using Wpf.Ui.Abstractions;
using Wpf.Ui.Controls;
using EasiLoginer.Presentation.Pages;

namespace EasiLoginer.Presentation.Shell;

/// <summary>
/// 主窗口：FluentWindow + 左侧 NavigationView 导航（配置 / 档案 / 更新 / 关于）。
/// </summary>
public partial class MainWindow : FluentWindow
{
    private readonly AppServices _services;

    /// <summary>创建主窗口。</summary>
    public MainWindow(AppServices services)
    {
        ArgumentNullException.ThrowIfNull(services);

        _services = services;
        InitializeComponent();

        // 高 DPI 下屏幕的逻辑尺寸可能小于默认窗口尺寸，这里夹到工作区内
        var workArea = SystemParameters.WorkArea;
        MaxWidth = workArea.Width;
        MaxHeight = workArea.Height;
        Width = Math.Min(Width, workArea.Width);
        Height = Math.Min(Height, workArea.Height);

        RootNavigation.SetPageProviderService(new PageProvider(services));
    }

    /// <summary>打开首次设置向导；完成后按用户选择跳到配置页或档案页。</summary>
    public void ShowOobe()
    {
        var wizard = new Onboarding.OobeWizardWindow(_services.Config) { Owner = this };
        var completed = wizard.ShowDialog() == true;

        RootNavigation.Navigate(completed && wizard.FinishToProfile
            ? typeof(ProfilePage)
            : typeof(ConfigPage));
    }

    /// <inheritdoc />
    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);

        // NavigationView 初始化时可能默认选中 footer 项（关于），
        // 因此在首帧渲染完成后（Idle）再显式导航到配置页。
        Dispatcher.BeginInvoke(
            new Action(() => RootNavigation.Navigate(typeof(ConfigPage))),
            DispatcherPriority.ApplicationIdle);
    }
}

/// <summary>导航页面工厂：把服务注入各页面（WPF-UI 的 DI 导航钩子）。</summary>
internal sealed class PageProvider(AppServices services) : INavigationViewPageProvider
{
    public object? GetPage(Type pageType) => pageType switch
    {
        var type when type == typeof(ConfigPage) => new ConfigPage(services),
        var type when type == typeof(ProfilePage) => new ProfilePage(services),
        var type when type == typeof(UpdatePage) => new UpdatePage(services),
        var type when type == typeof(AboutPage) => new AboutPage(),
        _ => null,
    };
}

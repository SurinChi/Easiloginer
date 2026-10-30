using System.Windows;
using Wpf.Ui.Appearance;
using Wpf.Ui.Markup;
using EasiLoginer.Presentation.Shell;

namespace EasiLoginer.Presentation;

/// <summary>
/// 应用入口：由宿主创建并注入服务后运行。
/// </summary>
/// <remarks>
/// 用代码构造 <see cref="System.Windows.Application"/> 而不是 <c>App.xaml</c>：
/// 本项目是类库（可执行文件是 Host），WPF 不允许库项目声明 <c>ApplicationDefinition</c>。
/// 资源字典在这里手动合并，效果与 XAML 等价。
/// </remarks>
public sealed class App : System.Windows.Application
{
    /// <summary>创建应用。</summary>
    /// <param name="services">宿主提供的服务。</param>
    public App(AppServices services)
    {
        ArgumentNullException.ThrowIfNull(services);

        Services = services;

        Resources.MergedDictionaries.Add(new ThemesDictionary { Theme = ApplicationTheme.Light });
        Resources.MergedDictionaries.Add(new ControlsDictionary());

        ShutdownMode = ShutdownMode.OnMainWindowClose;
    }

    /// <summary>注入的服务。</summary>
    public AppServices Services { get; }

    /// <summary>在已运行的主窗口上重新打开首次向导（供单实例转发调用）。</summary>
    public void ShowOobeOnMainWindow()
    {
        if (MainWindow is MainWindow window)
        {
            window.ShowOobe();
        }
    }

    /// <inheritdoc />
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ThemeService.Apply(Services.Config.Current.App.Theme);

        var window = new MainWindow(Services);
        MainWindow = window;
        window.Show();

        // 首次运行（或 IsOobeCompleted=false）时先走 6 步向导
        if (!Services.Config.Current.Internal.IsOobeCompleted)
        {
            window.ShowOobe();
        }
    }
}

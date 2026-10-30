using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using EasiLoginer.Application.Update;
using EasiLoginer.Domain.Versioning;

namespace EasiLoginer.Presentation.Pages;

/// <summary>更新页：版本、检查更新，以及「更新设置」分组（从配置页迁来）。</summary>
public partial class UpdatePage : Page
{
    private readonly AppServices? _services;
    private readonly UpdatePageViewModel? _viewModel;

    /// <summary>创建更新页。</summary>
    public UpdatePage(AppServices? services = null)
    {
        InitializeComponent();

        _services = services;
        if (services is not null)
        {
            _viewModel = new UpdatePageViewModel(services.Config);
            DataContext = _viewModel;
            PageLayout.ConstrainToNavigationHost(this);
        }

        VersionText.Text = $"当前版本：{CurrentVersionText()}";
        StatusText.Text = "点击「检查更新」从 GitHub Releases 获取最新版本";
    }

    private void OnSaveSettingsClicked(object sender, RoutedEventArgs e)
    {
        _viewModel?.Save();
    }

    private async void OnCheckClicked(object sender, RoutedEventArgs e)
    {
        if (_services is null)
        {
            StatusText.Text = "更新服务未注入";
            return;
        }

        CheckButton.IsEnabled = false;
        StatusText.Text = "正在检查……";

        try
        {
            var version = Pep440Version.Parse(CurrentVersionText());
            var channel = _services.Config.Current.Update.TargetUpdateChannel;
            var checker = new UpdateChecker(_services.Update);
            var result = await checker.CheckAsync(version, channel, CancellationToken.None);
            StatusText.Text = result.Message;
        }
        catch (FormatException)
        {
            StatusText.Text = "当前版本号无法解析，已跳过检查";
        }
        finally
        {
            CheckButton.IsEnabled = true;
        }
    }

    private static string CurrentVersionText()
    {
        var assembly = typeof(UpdatePage).Assembly;
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informational))
        {
            // 只取版本本身，忽略可能的 "+<commit>" 后缀
            return informational.Split('+')[0];
        }

        var version = assembly.GetName().Version;
        return version is null ? "0.0.1" : $"{version.Major}.{version.Minor}.{version.Build}";
    }
}

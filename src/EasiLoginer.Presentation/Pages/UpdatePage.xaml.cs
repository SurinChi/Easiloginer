using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using EasiLoginer.Application.Update;
using EasiLoginer.Domain.Versioning;
using EasiLoginer.Presentation.Update;

namespace EasiLoginer.Presentation.Pages;

/// <summary>更新页：显示当前版本并按配置通道检查更新。</summary>
public partial class UpdatePage : Page
{
    private readonly AppServices? _services;

    /// <summary>创建更新页。</summary>
    public UpdatePage(AppServices? services = null)
    {
        InitializeComponent();

        _services = services;
        VersionText.Text = $"当前版本：{CurrentVersionText()}";
        StatusText.Text = "尚未配置更新源";
    }

    private async void OnCheckClicked(object sender, RoutedEventArgs e)
    {
        if (_services is null)
        {
            StatusText.Text = "尚未配置更新源";
            return;
        }

        CheckButton.IsEnabled = false;
        StatusText.Text = "正在检查……";

        try
        {
            var version = Pep440Version.Parse(CurrentVersionText());
            var channel = _services.Config.Current.Update.TargetUpdateChannel;
            var checker = new UpdateChecker(new NotConfiguredUpdateSource());
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
            return informational;
        }

        var version = assembly.GetName().Version;
        return version is null ? "0.0.1" : $"{version.Major}.{version.Minor}.{version.Build}";
    }
}

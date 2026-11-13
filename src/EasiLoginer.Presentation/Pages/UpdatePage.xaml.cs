using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using EasiLoginer.Application.Update;
using EasiLoginer.Domain.Update;
using EasiLoginer.Domain.Versioning;

namespace EasiLoginer.Presentation.Pages;

/// <summary>更新页：版本、检查/下载/启动安装，以及「更新设置」分组（从设置页迁来）。</summary>
public partial class UpdatePage : Page
{
    private readonly AppServices? _services;
    private readonly UpdatePageViewModel? _viewModel;

    private UpdateAsset? _pendingAsset;
    private string? _downloadedPath;

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

        VersionText.Text = $"当前版本：{AppVersion.Text}";
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

        ResetDownloadState();
        CheckButton.IsEnabled = false;
        StatusText.Text = "正在检查……";

        try
        {
            var version = Pep440Version.Parse(AppVersion.Text);
            var channel = _services.Config.Current.Update.TargetUpdateChannel;
            var checker = new UpdateChecker(_services.Update);
            var result = await checker.CheckAsync(version, channel, CancellationToken.None);
            StatusText.Text = result.Message;

            if (result.Availability == UpdateAvailability.Available && result.Manifest is not null)
            {
                _pendingAsset = PickAsset(result.Manifest);
                if (_pendingAsset is null)
                {
                    StatusText.Text = $"{result.Message}（该版本没有可下载的安装包）";
                    return;
                }

                DownloadButton.Content = $"下载 {result.Manifest.Version}";
                DownloadButton.Visibility = Visibility.Visible;
            }
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

    private async void OnDownloadClicked(object sender, RoutedEventArgs e)
    {
        if (_services is null || _pendingAsset is null)
        {
            return;
        }

        DownloadButton.IsEnabled = false;
        DownloadProgress.Visibility = Visibility.Visible;
        DownloadProgress.Value = 0;
        StatusText.Text = "正在下载……";

        var progress = new Progress<UpdateDownloadProgress>(report =>
        {
            if (report.Total > 0)
            {
                DownloadProgress.Value = report.Received * 100.0 / report.Total;
            }

            StatusText.Text = report.Total > 0
                ? $"正在下载…… {report.Received / 1024.0 / 1024.0:0.0} / {report.Total / 1024.0 / 1024.0:0.0} MB"
                : $"正在下载…… {report.Received / 1024.0 / 1024.0:0.0} MB";
        });

        try
        {
            var result = await _services.Downloader.DownloadAsync(_pendingAsset, progress, CancellationToken.None);
            _downloadedPath = result.FilePath;
            DownloadProgress.Value = 100;

            // 只有清单确实给了 SHA-256 才能说「通过校验」
            StatusText.Text = string.IsNullOrWhiteSpace(_pendingAsset.Sha256)
                ? $"已下载（发布方未提供 SHA-256，未做摘要校验）：{result.FilePath}"
                : $"已下载并通过校验：{result.FilePath}";
            DownloadButton.Visibility = Visibility.Collapsed;
            InstallButton.Visibility = Visibility.Visible;
        }
        catch (IOException exception)
        {
            DownloadProgress.Visibility = Visibility.Collapsed;
            StatusText.Text = $"下载失败：{exception.Message}";
            DownloadButton.IsEnabled = true;
        }
        catch (UnauthorizedAccessException exception)
        {
            DownloadProgress.Visibility = Visibility.Collapsed;
            StatusText.Text = $"下载失败：{exception.Message}";
            DownloadButton.IsEnabled = true;
        }
    }

    private void OnInstallClicked(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_downloadedPath) || !File.Exists(_downloadedPath))
        {
            StatusText.Text = "下载文件已不存在，请重新下载。";
            return;
        }

        // 是安装包就直接运行；是绿色版 ZIP 就打开所在文件夹
        if (string.Equals(Path.GetExtension(_downloadedPath), ".exe", StringComparison.OrdinalIgnoreCase))
        {
            Process.Start(new ProcessStartInfo(_downloadedPath) { UseShellExecute = true });
            StatusText.Text = "已启动安装程序。";
            return;
        }

        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{_downloadedPath}\"") { UseShellExecute = true });
        StatusText.Text = "已打开下载文件所在位置。";
    }

    private void ResetDownloadState()
    {
        _pendingAsset = null;
        _downloadedPath = null;
        DownloadButton.Visibility = Visibility.Collapsed;
        DownloadButton.IsEnabled = true;
        InstallButton.Visibility = Visibility.Collapsed;
        DownloadProgress.Visibility = Visibility.Collapsed;
        DownloadProgress.Value = 0;
    }

    /// <summary>优先选安装包（Setup exe），没有就选 ZIP。</summary>
    private static UpdateAsset? PickAsset(UpdateManifest manifest)
    {
        var assets = manifest.Assets;
        return assets.FirstOrDefault(asset =>
                   asset.Url.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
                   asset.Url.Contains("Setup", StringComparison.OrdinalIgnoreCase))
               ?? assets.FirstOrDefault(asset => asset.Url.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
               ?? assets.FirstOrDefault(asset => asset.Url.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));
    }
}

using System.IO;
using System.Windows;

namespace EasiLoginer.Presentation.Installer;

/// <summary>安装向导：SFX 解压后由 <c>EasiLoginer.exe --install</c> 打开。</summary>
public partial class InstallWizardWindow : Window
{
    private bool _installing;
    private bool _installed;
    private string? _installedTo;

    /// <summary>创建安装向导。</summary>
    public InstallWizardWindow()
    {
        InitializeComponent();

        TargetDirectoryBox.Text = InstallPayloadService.DefaultTarget;
        StatusText.Text = "准备安装";
    }

    private void OnBrowseClicked(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "选择安装目录",
            InitialDirectory = Directory.Exists(TargetDirectoryBox.Text)
                ? TargetDirectoryBox.Text
                : Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        };

        if (dialog.ShowDialog(this) == true)
        {
            TargetDirectoryBox.Text = dialog.FolderName;
        }
    }

    private async void OnInstallClicked(object sender, RoutedEventArgs e)
    {
        if (_installed)
        {
            Shutdown();
            return;
        }

        if (_installing)
        {
            return;
        }

        var target = TargetDirectoryBox.Text.Trim();
        try
        {
            InstallPayloadService.ValidateTarget(target);
        }
        catch (ArgumentException exception)
        {
            MessageBox.Show(this, exception.Message, "安装目录无效", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _installing = true;
        InstallButton.IsEnabled = false;
        CancelButton.IsEnabled = false;
        SubtitleText.Text = "正在安装…";

        var options = new InstallOptions(
            target,
            DesktopShortcutCheck.IsChecked == true,
            StartMenuShortcutCheck.IsChecked == true,
            LaunchCheck.IsChecked == true);

        var progress = new Progress<InstallProgress>(report =>
        {
            InstallProgressBar.Value = report.Total == 0 ? 0 : report.Completed * 100.0 / report.Total;
            StatusText.Text = $"{report.Completed} / {report.Total}  {report.CurrentFile}";
        });

        try
        {
            _installedTo = await Task.Run(() => InstallPayloadService.Install(options, progress));

            _installing = false;
            _installed = true;
            InstallProgressBar.Value = 100;
            SubtitleText.Text = "安装完成";
            StatusText.Text = $"已安装到 {_installedTo}";
            InstallButton.Content = "完成";
            InstallButton.IsEnabled = true;
            CancelButton.Visibility = Visibility.Collapsed;

            if (options.LaunchAfterInstall)
            {
                InstallPayloadService.Launch(_installedTo);
            }

            InstallPayloadService.CleanupSource(_installedTo);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _installing = false;
            InstallButton.IsEnabled = true;
            CancelButton.IsEnabled = true;
            SubtitleText.Text = "安装失败";
            StatusText.Text = exception.Message;

            MessageBox.Show(this, $"安装失败：{exception.Message}", "安装失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnCancelClicked(object sender, RoutedEventArgs e) => Shutdown();

    private void Shutdown()
    {
        if (System.Windows.Application.Current is not null)
        {
            System.Windows.Application.Current.Shutdown(_installed ? 0 : 1);
            return;
        }

        Close();
    }
}

using System.Diagnostics;
using System.IO;
using System.Windows;

namespace EasiLoginer.Installer;

/// <summary>安装向导窗口：选择目录 → 选项 → 解压安装 → 完成。</summary>
public partial class InstallerWindow : Window
{
    private int _step;
    private bool _installing;
    private bool _installed;
    private InstallOptions? _options;

    /// <summary>创建向导窗口。</summary>
    public InstallerWindow()
    {
        InitializeComponent();

        TargetDirectoryBox.Text = InstallService.DefaultDirectory;
        StatusText.Text = InstallService.OpenPayload() is null
            ? "此安装包未包含程序文件"
            : string.Empty;

        UpdateStep();
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

    private async void OnNextClicked(object sender, RoutedEventArgs e)
    {
        switch (_step)
        {
            case 0:
                if (!ValidateDirectory())
                {
                    return;
                }

                _step = 1;
                UpdateStep();
                return;

            case 1:
                await StartInstallAsync();
                return;

            default:
                Close();
                return;
        }
    }

    private void OnBackClicked(object sender, RoutedEventArgs e)
    {
        if (_step > 0 && !_installing && !_installed)
        {
            _step--;
            UpdateStep();
        }
    }

    private void OnCancelClicked(object sender, RoutedEventArgs e)
    {
        if (_installing)
        {
            return;
        }

        if (_installed)
        {
            Close();
            return;
        }

        var result = MessageBox.Show(
            this,
            "确定要取消安装吗？",
            "EasiLoginer 安装向导",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            Close();
        }
    }

    private bool ValidateDirectory()
    {
        try
        {
            InstallService.ValidateTarget(TargetDirectoryBox.Text);

            if (InstallService.OpenPayload() is null)
            {
                MessageBox.Show(
                    this,
                    "此安装包没有内置程序文件，可能是开发构建。请使用发布脚本生成的安装包。",
                    "无法安装",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            return true;
        }
        catch (ArgumentException exception)
        {
            MessageBox.Show(this, exception.Message, "安装目录无效", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
    }

    private async Task StartInstallAsync()
    {
        _options = new InstallOptions(
            TargetDirectoryBox.Text.Trim(),
            DesktopShortcutCheck.IsChecked == true,
            StartMenuShortcutCheck.IsChecked == true,
            LaunchCheck.IsChecked == true);

        _installing = true;
        _step = 2;
        UpdateStep();

        var progress = new Progress<InstallProgress>(report =>
        {
            InstallProgressBar.Value = report.Total == 0 ? 0 : report.Completed * 100.0 / report.Total;
            ProgressDetail.Text = $"{report.Completed} / {report.Total}  {report.CurrentFile}";
        });

        try
        {
            await Task.Run(() => InstallService.Install(_options, progress));

            _installing = false;
            _installed = true;
            _step = 3;

            ProgressTitle.Text = "安装完成";
            InstallProgressBar.Value = 100;
            FinishTitle.Text = "安装完成";
            FinishDetail.Text =
                $"EasiLoginer 已安装到：\n{_options.TargetDirectory}\n\n" +
                "快捷方式与卸载项已创建，可在「设置 → 应用」中卸载。";
            StatusText.Text = string.Empty;
            UpdateStep();

            if (_options.LaunchAfterInstall)
            {
                LaunchApp(_options.TargetDirectory);
            }
        }
        catch (Exception exception)
        {
            _installing = false;
            _step = 1;
            UpdateStep();

            MessageBox.Show(this, $"安装失败：{exception.Message}", "安装失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static void LaunchApp(string directory)
    {
        var executable = Path.Combine(directory, InstallService.AppExecutable);
        if (!File.Exists(executable))
        {
            return;
        }

        Process.Start(new ProcessStartInfo(executable)
        {
            WorkingDirectory = directory,
            UseShellExecute = true,
        });
    }

    private void UpdateStep()
    {
        LicenseStep.Visibility = _step == 0 ? Visibility.Visible : Visibility.Collapsed;
        OptionsStep.Visibility = _step == 1 ? Visibility.Visible : Visibility.Collapsed;
        ProgressStep.Visibility = _step == 2 ? Visibility.Visible : Visibility.Collapsed;
        FinishStep.Visibility = _step == 3 ? Visibility.Visible : Visibility.Collapsed;

        BackButton.IsEnabled = _step is 1 && !_installing;
        CancelButton.IsEnabled = !_installing;
        CancelButton.Content = _installed ? "关闭" : "取消";
        NextButton.Content = _step switch
        {
            0 => "下一步",
            1 => "安装",
            _ => "完成",
        };
        NextButton.IsEnabled = !_installing;

        SubtitleText.Text = _step switch
        {
            0 => "第 1 步，共 3 步 · 选择安装位置",
            1 => "第 2 步，共 3 步 · 安装选项",
            2 => "正在安装…",
            _ => "安装完成",
        };
    }


}

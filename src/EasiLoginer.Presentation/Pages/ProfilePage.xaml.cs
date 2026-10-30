using System.Windows;
using System.Windows.Controls;
using EasiLoginer.Presentation.Dialogs;

namespace EasiLoginer.Presentation.Pages;

/// <summary>档案页：加密状态、列表 + 编辑器 + 「运行登录」（唯一的 UI 触发入口）。</summary>
public partial class ProfilePage : Page
{
    private readonly ProfilePageViewModel _viewModel;

    /// <summary>创建档案页。</summary>
    public ProfilePage(AppServices services)
    {
        ArgumentNullException.ThrowIfNull(services);

        InitializeComponent();
        _viewModel = new ProfilePageViewModel(services.Profiles, services.Login, services.Config, services.Display);
        DataContext = _viewModel;
        PageLayout.ConstrainToNavigationHost(this);
    }

    private void OnAddClicked(object sender, RoutedEventArgs e) => _viewModel.Add();

    private void OnDeleteClicked(object sender, RoutedEventArgs e)
    {
        if (!EnsureMasterPassword())
        {
            return;
        }

        _viewModel.Delete();
        _viewModel.EndSensitiveOperation();
    }

    private void OnSaveClicked(object sender, RoutedEventArgs e)
    {
        if (!EnsureMasterPassword())
        {
            return;
        }

        _viewModel.Save();
        _viewModel.EndSensitiveOperation();
    }

    private async void OnRunClicked(object sender, RoutedEventArgs e)
    {
        if (!EnsureMasterPassword())
        {
            return;
        }

        await _viewModel.RunAsync();
        _viewModel.EndSensitiveOperation();
    }

    /// <summary>涉及账号密码的操作前验证主密码；未设主密码时直接放行。</summary>
    private bool EnsureMasterPassword()
    {
        if (!_viewModel.RequiresMasterPassword)
        {
            return true;
        }

        var dialog = new MasterPasswordDialog(
            "验证主密码",
            "该操作涉及账号与密码，请先输入主密码。",
            requireConfirmation: false)
        {
            Owner = Window.GetWindow(this),
        };

        if (dialog.ShowDialog() != true)
        {
            return false;
        }

        if (_viewModel.VerifyMasterPassword(dialog.Password))
        {
            return true;
        }

        MessageBox.Show(
            Window.GetWindow(this),
            "主密码错误，操作已取消。",
            "验证失败",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);

        return false;
    }

    private void OnEnableEncryptionClicked(object sender, RoutedEventArgs e)
    {
        var dialog = new MasterPasswordDialog(
            "启用档案加密",
            "设置主密码后，档案中的账号与密码会以密文保存。忘记主密码将无法恢复这些密码。",
            requireConfirmation: true)
        {
            Owner = Window.GetWindow(this),
        };

        if (dialog.ShowDialog() == true)
        {
            _viewModel.EnableEncryption(dialog.Password);

            // 启用后立即回到锁定状态：后续每次敏感操作都要重新验证
            _viewModel.EndSensitiveOperation();
        }
    }

    private void OnUnlockClicked(object sender, RoutedEventArgs e)
    {
        var dialog = new MasterPasswordDialog("解锁档案", "输入主密码以查看与修改档案。", requireConfirmation: false)
        {
            Owner = Window.GetWindow(this),
        };

        if (dialog.ShowDialog() == true)
        {
            _viewModel.Unlock(dialog.Password);
        }
    }

    private void OnDisableEncryptionClicked(object sender, RoutedEventArgs e)
    {
        var dialog = new MasterPasswordDialog(
            "关闭档案加密",
            "校验主密码后，档案会恢复为明文保存。",
            requireConfirmation: false)
        {
            Owner = Window.GetWindow(this),
        };

        if (dialog.ShowDialog() == true)
        {
            _viewModel.DisableEncryption(dialog.Password);
        }
    }
}

using System.Windows;
using System.Windows.Controls;
using EasiLoginer.Presentation.Dialogs;

namespace EasiLoginer.Presentation.Pages;

/// <summary>档案页：加密状态、列表 + 编辑器 + 「运行登录」（唯一的 UI 触发入口）。</summary>
public partial class ProfilePage : Page
{
    private readonly ProfilePageViewModel _viewModel;

    /// <summary>正在把视图模型的密码写进 PasswordBox 时抑制反向同步。</summary>
    private bool _syncingPassword;

    /// <summary>创建档案页。</summary>
    public ProfilePage(AppServices services)
    {
        ArgumentNullException.ThrowIfNull(services);

        InitializeComponent();
        _viewModel = new ProfilePageViewModel(services.Profiles, services.Login, services.Config, services.Display);
        DataContext = _viewModel;
        PageLayout.ConstrainToNavigationHost(this);

        _viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(ProfilePageViewModel.Selected))
            {
                SyncPasswordBox();
            }
        };
        SyncPasswordBox();
    }

    /// <summary>把当前选中档案的密码同步到两个输入框（避免来回触发变更事件）。</summary>
    private void SyncPasswordBox()
    {
        if (PasswordInput is null)
        {
            return;
        }

        _syncingPassword = true;
        try
        {
            var password = _viewModel.Selected?.Password ?? string.Empty;
            PasswordInput.Password = password;
            PasswordPlain.Text = password;
        }
        finally
        {
            _syncingPassword = false;
        }
    }

    private void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (_syncingPassword || _viewModel.Selected is null)
        {
            return;
        }

        _viewModel.Selected.Password = PasswordInput.Password;

        _syncingPassword = true;
        try
        {
            PasswordPlain.Text = PasswordInput.Password;
        }
        finally
        {
            _syncingPassword = false;
        }
    }

    private void OnPlainTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_syncingPassword || _viewModel.Selected is null)
        {
            return;
        }

        _viewModel.Selected.Password = PasswordPlain.Text;

        _syncingPassword = true;
        try
        {
            PasswordInput.Password = PasswordPlain.Text;
        }
        finally
        {
            _syncingPassword = false;
        }
    }

    /// <summary>在 PasswordBox 与明文 TextBox 之间切换；两者始终保存同一份密码。</summary>
    private void OnRevealClicked(object sender, RoutedEventArgs e)
    {
        var reveal = PasswordInput.Visibility == Visibility.Visible;

        _syncingPassword = true;
        try
        {
            if (reveal)
            {
                PasswordPlain.Text = PasswordInput.Password;
            }
            else
            {
                PasswordInput.Password = PasswordPlain.Text;
            }
        }
        finally
        {
            _syncingPassword = false;
        }

        PasswordInput.Visibility = reveal ? Visibility.Collapsed : Visibility.Visible;
        PasswordPlain.Visibility = reveal ? Visibility.Visible : Visibility.Collapsed;
        RevealButton.Content = reveal ? "🙈" : "👁";
        RevealButton.ToolTip = reveal ? "隐藏密码" : "显示密码";

        if (reveal)
        {
            PasswordPlain.Focus();
            PasswordPlain.CaretIndex = PasswordPlain.Text.Length;
        }
    }

    private void OnAddClicked(object sender, RoutedEventArgs e) => _viewModel.Add();

    private void OnDeleteClicked(object sender, RoutedEventArgs e)
    {
        // 删除账号属于"修改账号"，设了主密码就要验证
        if (_viewModel.RequiresMasterPassword && !EnsureMasterPassword())
        {
            return;
        }

        _viewModel.Delete();
        _viewModel.CaptureSensitiveSnapshot();
    }

    private void OnSaveClicked(object sender, RoutedEventArgs e)
    {
        // 只有账号或密码真的被改过才要求验证；改名称等其它字段直接保存
        if (_viewModel.RequiresMasterPassword && _viewModel.HasSensitiveChanges() && !EnsureMasterPassword())
        {
            return;
        }

        _viewModel.Save();
        _viewModel.CaptureSensitiveSnapshot();
        SyncPasswordBox();
    }

    private async void OnRunClicked(object sender, RoutedEventArgs e) => await _viewModel.RunAsync();

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
            _viewModel.CaptureSensitiveSnapshot();
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
            SyncPasswordBox();
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
            SyncPasswordBox();
        }
    }
}

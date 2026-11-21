using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Controls;

namespace EasiLoginer.Presentation.Dialogs;

/// <summary>修改账号/密码时选择的验证方式。</summary>
public enum CredentialVerificationMethod
{
    /// <summary>验证主密码。</summary>
    MasterPassword,

    /// <summary>验证当前档案的原密码。</summary>
    ProfilePassword,
}

/// <summary>
/// 修改账号/密码前的凭据验证弹窗：顶部切换「主密码」与「档案原密码」两页。
/// </summary>
public partial class CredentialVerificationDialog : FluentWindow
{
    /// <summary>创建验证弹窗。</summary>
    /// <param name="canVerifyProfilePassword">当前档案是否已有可验证的原密码。</param>
    public CredentialVerificationDialog(bool canVerifyProfilePassword)
    {
        InitializeComponent();

        ProfilePasswordTab.IsEnabled = canVerifyProfilePassword;
        ProfilePasswordTab.ToolTip = canVerifyProfilePassword
            ? "使用当前档案已保存的密码验证"
            : "该档案没有已保存的密码，不能使用此方式";

        Loaded += (_, _) => FocusCurrentTab();
    }

    /// <summary>用户选择的验证方式。</summary>
    public CredentialVerificationMethod Method { get; private set; }

    /// <summary>用户输入的密码；仅在 <see cref="Window.ShowDialog"/> 返回 true 时有效。</summary>
    public string Password { get; private set; } = string.Empty;

    private void OnMethodChanged(object sender, SelectionChangedEventArgs e) => FocusCurrentTab();

    private void FocusCurrentTab()
    {
        if (!IsLoaded)
        {
            return;
        }

        if (MethodTabs.SelectedIndex == 1 && ProfilePasswordTab.IsEnabled)
        {
            ProfilePasswordInput.Focus();
        }
        else
        {
            MasterPasswordInput.Focus();
        }
    }

    private void OnConfirmClicked(object sender, RoutedEventArgs e)
    {
        var useProfilePassword = MethodTabs.SelectedIndex == 1;
        var password = useProfilePassword ? ProfilePasswordInput.Password : MasterPasswordInput.Password;

        if (string.IsNullOrEmpty(password))
        {
            ErrorText.Text = useProfilePassword ? "档案原密码不能为空。" : "主密码不能为空。";
            return;
        }

        Method = useProfilePassword
            ? CredentialVerificationMethod.ProfilePassword
            : CredentialVerificationMethod.MasterPassword;
        Password = password;
        DialogResult = true;
    }

    private void OnCancelClicked(object sender, RoutedEventArgs e) => DialogResult = false;
}

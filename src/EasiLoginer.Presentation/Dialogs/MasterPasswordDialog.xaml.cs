using System.Windows;
using Wpf.Ui.Controls;

namespace EasiLoginer.Presentation.Dialogs;

/// <summary>
/// 主密码输入对话框：用于解锁、启用加密（含二次确认）与关闭加密。
/// </summary>
public partial class MasterPasswordDialog : FluentWindow
{
    private readonly bool _requireConfirmation;

    /// <summary>创建对话框。</summary>
    /// <param name="title">标题。</param>
    /// <param name="message">说明文字。</param>
    /// <param name="requireConfirmation">是否需要二次确认（启用加密时）。</param>
    public MasterPasswordDialog(string title, string message, bool requireConfirmation)
    {
        InitializeComponent();

        Title = title;
        Header.Title = title;
        MessageText.Text = message;
        _requireConfirmation = requireConfirmation;
        ConfirmPanel.Visibility = requireConfirmation ? Visibility.Visible : Visibility.Collapsed;

        Loaded += (_, _) => PasswordInput.Focus();
    }

    /// <summary>用户输入的密码；仅在 <see cref="Window.ShowDialog"/> 返回 true 时有效。</summary>
    public string Password { get; private set; } = string.Empty;

    private void OnPasswordChanged(object sender, RoutedEventArgs e) => ErrorText.Text = string.Empty;

    private void OnConfirmClicked(object sender, RoutedEventArgs e)
    {
        var password = PasswordInput.Password;

        if (string.IsNullOrEmpty(password))
        {
            ErrorText.Text = "主密码不能为空。";
            return;
        }

        if (_requireConfirmation && !string.Equals(password, ConfirmInput.Password, StringComparison.Ordinal))
        {
            ErrorText.Text = "两次输入的主密码不一致。";
            return;
        }

        Password = password;
        DialogResult = true;
    }

    private void OnCancelClicked(object sender, RoutedEventArgs e) => DialogResult = false;
}

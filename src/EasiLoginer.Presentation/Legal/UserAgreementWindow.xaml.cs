using System.Windows;
using Wpf.Ui.Controls;

namespace EasiLoginer.Presentation.Legal;

/// <summary>用户协议查看窗口（关于页入口）。</summary>
public partial class UserAgreementWindow : FluentWindow
{
    /// <summary>创建协议窗口。</summary>
    public UserAgreementWindow()
    {
        InitializeComponent();

        AgreementText.Text = UserAgreement.Text;
        VersionText.Text = $"版本 {UserAgreement.Version} · 生效日期 {UserAgreement.EffectiveDate}";
    }

    private void OnCloseClicked(object sender, RoutedEventArgs e) => Close();
}

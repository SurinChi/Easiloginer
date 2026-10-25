using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace EasiLoginer.Presentation.Pages;

/// <summary>关于页：版本、上游归属、反馈与许可信息（文档 §10.2）。</summary>
public partial class AboutPage : Page
{
    private const string FeedbackAddress = "hiloading@163.com";

    private readonly string _version;

    /// <summary>创建关于页。</summary>
    public AboutPage()
    {
        InitializeComponent();

        var assembly = typeof(AboutPage).Assembly;
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        // 兜底：历史构建可能带 "+<commit>"，只取版本本身
        _version = informational?.Split('+')[0] ?? assembly.GetName().Version?.ToString() ?? "未知";
        VersionText.Text = $"版本 {_version} · GPLv3";
    }

    private void OnSendFeedbackClicked(object sender, RoutedEventArgs e)
    {
        // 不直接连 SMTP：既没有他人凭据，也容易被当成垃圾邮件来源。
        // 用 mailto 交给系统默认邮件客户端，用户能看到并确认实际发送的内容。
        var subject = Uri.EscapeDataString($"EasiLoginer {_version} 反馈");
        var body = Uri.EscapeDataString(
            $"应用版本：{_version}{Environment.NewLine}" +
            $"系统：{Environment.OSVersion}{Environment.NewLine}" +
            $"架构：{Environment.Is64BitProcess switch { true => "x64", false => "x86" }}{Environment.NewLine}{Environment.NewLine}" +
            $"问题描述：{Environment.NewLine}{Environment.NewLine}" +
            $"复现步骤：{Environment.NewLine}{Environment.NewLine}");

        try
        {
            Process.Start(new ProcessStartInfo($"mailto:{FeedbackAddress}?subject={subject}&body={body}")
            {
                UseShellExecute = true,
            });

            FeedbackStatus.Text = $"已打开邮件客户端，收件人 {FeedbackAddress}";
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            FeedbackStatus.Text = $"没有可用的邮件客户端，请手动发送到 {FeedbackAddress}";
        }
    }

    private void OnCopyEmailClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(FeedbackAddress);
            FeedbackStatus.Text = $"已复制邮箱：{FeedbackAddress}";
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            FeedbackStatus.Text = $"复制失败，请手动记下 {FeedbackAddress}";
        }
    }
}

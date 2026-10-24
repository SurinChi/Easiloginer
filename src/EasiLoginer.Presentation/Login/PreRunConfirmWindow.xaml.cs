using System.Windows;
using System.Windows.Threading;
using Wpf.Ui.Controls;
using EasiLoginer.Application.Automation;

namespace EasiLoginer.Presentation.Login;

/// <summary>
/// 运行前确认弹窗：倒计时到 0 自动继续，可推迟或取消（文档 §10.5）。
/// </summary>
public partial class PreRunConfirmWindow : FluentWindow
{
    private readonly DispatcherTimer _timer;
    private int _remaining;

    /// <summary>创建弹窗。</summary>
    public PreRunConfirmWindow(LoginConfirmationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        InitializeComponent();

        AccountText.Text = string.IsNullOrEmpty(request.AccountName)
            ? "即将运行希沃白板自动登录"
            : $"即将为 {request.AccountName} 运行希沃白板自动登录";

        _remaining = Math.Max(0, request.TimeoutSeconds);
        CountdownText.Text = $"若 {_remaining} 秒内无操作将自动继续";

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += OnTick;
        _timer.Start();
    }

    /// <summary>用户的选择；倒计时结束视为立即执行。</summary>
    public LoginConfirmationChoice Choice { get; private set; } = LoginConfirmationChoice.Proceed;

    private void OnTick(object? sender, EventArgs e)
    {
        _remaining--;

        if (_remaining <= 0)
        {
            Finish(LoginConfirmationChoice.Proceed);
            return;
        }

        CountdownText.Text = $"若 {_remaining} 秒内无操作将自动继续";
    }

    private void OnProceedClicked(object sender, RoutedEventArgs e) => Finish(LoginConfirmationChoice.Proceed);

    private void OnDelayClicked(object sender, RoutedEventArgs e) => Finish(LoginConfirmationChoice.Delay);

    private void OnCancelClicked(object sender, RoutedEventArgs e) => Finish(LoginConfirmationChoice.Cancel);

    private void Finish(LoginConfirmationChoice choice)
    {
        _timer.Stop();
        Choice = choice;
        Close();
    }
}

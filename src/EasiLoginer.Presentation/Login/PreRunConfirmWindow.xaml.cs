using System.ComponentModel;
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
    private bool _finished;

    /// <summary>创建弹窗。</summary>
    public PreRunConfirmWindow(LoginConfirmationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        InitializeComponent();

        AccountText.Text = string.IsNullOrEmpty(request.AccountName)
            ? "即将运行希沃白板自动登录"
            : $"即将为 {request.AccountName} 运行希沃白板自动登录";

        _remaining = Math.Max(0, request.TimeoutSeconds);
        UpdateCountdown();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += OnTick;
        _timer.Start();
    }

    /// <summary>
    /// 用户的选择。默认是取消：只有经 <see cref="Finish"/> 明确选择才会变成「立即执行」，
    /// 关闭窗口（Alt+F4、主窗口被关闭等）一律按取消处理。
    /// </summary>
    public LoginConfirmationChoice Choice { get; private set; } = LoginConfirmationChoice.Cancel;

    private void OnTick(object? sender, EventArgs e)
    {
        _remaining--;

        if (_remaining <= 0)
        {
            Finish(LoginConfirmationChoice.Proceed);
            return;
        }

        UpdateCountdown();
    }

    /// <inheritdoc />
    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);

        _timer.Stop();

        if (!_finished)
        {
            // 未点按钮就关闭：按取消处理，绝不能默默继续登录
            _finished = true;
            Choice = LoginConfirmationChoice.Cancel;
        }
    }

    /// <summary>倒计时直接显示在「立即执行」按钮上。</summary>
    private void UpdateCountdown()
        => ProceedButton.Content = _remaining > 0 ? $"立即执行（{_remaining}s）" : "立即执行";

    private void OnProceedClicked(object sender, RoutedEventArgs e) => Finish(LoginConfirmationChoice.Proceed);

    private void OnDelayClicked(object sender, RoutedEventArgs e) => Finish(LoginConfirmationChoice.Delay);

    private void OnCancelClicked(object sender, RoutedEventArgs e) => Finish(LoginConfirmationChoice.Cancel);

    private void Finish(LoginConfirmationChoice choice)
    {
        if (_finished)
        {
            return;
        }

        _finished = true;
        _timer.Stop();
        Choice = choice;

        // ShowDialog 只有 DialogResult 为 true 时才返回 true，供调用方区分「选择」与「直接关窗」
        try
        {
            DialogResult = true;
        }
        catch (InvalidOperationException)
        {
            // 非模态显示时 DialogResult 不可用，退化为普通关闭
            Close();
        }
    }
}

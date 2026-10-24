using System.Windows;
using System.Windows.Interop;
using EasiLoginer.Domain.Automation;

namespace EasiLoginer.Presentation.Login;

/// <summary>
/// 登录状态浮窗：置顶、不抢焦点、不进任务栏，显示当前任务与进度（文档 §10.5）。
/// </summary>
public partial class StatusOverlayWindow : Window
{
    private const int GwlExStyle = -20;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;

    /// <summary>创建浮窗并定位到主屏底部居中。</summary>
    public StatusOverlayWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => ApplyNoActivate();

        var workArea = SystemParameters.WorkArea;
        Left = workArea.Left + ((workArea.Width - Width) / 2);
        Top = workArea.Bottom - Height - 54;
    }

    /// <summary>更新任务标题。</summary>
    public void SetTask(string task) => TaskText.Text = task;

    /// <summary>更新进度文案。</summary>
    public void SetProgress(string progress) => ProgressText.Text = progress;

    /// <summary>展示最终结果。</summary>
    public void SetFinal(LoginOutcome outcome, string? error)
    {
        TaskText.Text = outcome switch
        {
            LoginOutcome.Success => "登录成功",
            LoginOutcome.Cancelled => "已取消",
            _ => "登录失败",
        };

        ProgressText.Text = outcome == LoginOutcome.Failed && !string.IsNullOrEmpty(error)
            ? error
            : string.Empty;
    }

    private void ApplyNoActivate()
    {
        var handle = new WindowInteropHelper(this).Handle;
        var style = NativeMethods.GetWindowLong(handle, GwlExStyle);
        _ = NativeMethods.SetWindowLong(handle, GwlExStyle, style | WsExToolWindow | WsExNoActivate);
    }
}

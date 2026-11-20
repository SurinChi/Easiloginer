using System.Threading;
using System.Windows;
using System.Windows.Controls;
using EasiLoginer.Presentation.Login;
using FluentAssertions;

namespace EasiLoginer.UiTests;

/// <summary>登录状态浮窗的「立即终止」入口。</summary>
public sealed class StatusOverlayWindowTests
{
    /// <summary>点击按钮必须触发取消事件，供登录管线取消 CancellationToken。</summary>
    [Fact]
    public void 立即终止按钮触发取消事件()
    {
        Exception? failure = null;

        var thread = new Thread(() =>
        {
            try
            {
                var window = new StatusOverlayWindow();
                var raised = false;
                window.CancelRequested += (_, _) => raised = true;

                var button = window.FindName("CancelButton") as Button;
                button.Should().NotBeNull("状态浮窗必须有「立即终止」按钮");
                button!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

                raised.Should().BeTrue();
                button.IsEnabled.Should().BeFalse("点击后按钮应禁用，避免重复取消");
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        failure.Should().BeNull();
    }
}

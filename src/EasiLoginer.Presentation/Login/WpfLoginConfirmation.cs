using System.Windows;
using EasiLoginer.Application.Automation;

namespace EasiLoginer.Presentation.Login;

/// <summary>用 WPF 弹窗实现运行前确认；保证在 UI 线程上显示。</summary>
public sealed class WpfLoginConfirmation : ILoginConfirmation
{
    /// <inheritdoc />
    public Task<LoginConfirmationChoice> ConfirmAsync(LoginConfirmationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var dispatcher = System.Windows.Application.Current?.Dispatcher;

        if (dispatcher is null || dispatcher.CheckAccess())
        {
            return Task.FromResult(Show(request));
        }

        return dispatcher.Invoke(() => Task.FromResult(Show(request)));
    }

    private static LoginConfirmationChoice Show(LoginConfirmationRequest request)
    {
        var window = new PreRunConfirmWindow(request);
        var owner = System.Windows.Application.Current?.MainWindow;

        if (owner is not null && owner.IsVisible && !ReferenceEquals(owner, window))
        {
            window.Owner = owner;
        }

        window.ShowDialog();
        return window.Choice;
    }
}

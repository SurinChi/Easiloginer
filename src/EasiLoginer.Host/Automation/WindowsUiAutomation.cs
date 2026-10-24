using System.Runtime.InteropServices;
using System.Windows.Automation;
using EasiLoginer.Automation.Uia;

namespace EasiLoginer.Host.Automation;

/// <summary>
/// 基于 Windows 自带 <see cref="System.Windows.Automation"/> 的实现。
/// </summary>
/// <remarks>
/// 没有选 FlaUI：本机开启了 Smart App Control，未签名第三方 DLL 可能在运行期被拦截；
/// <c>UIAutomationClient.dll</c> 由 Windows 签名，不需要随包分发。文档把内置实现列为备选。
/// 所有调用都在调用方线程完成；元素树按窗口缓存，窗口失效时自动重查。
/// </remarks>
public sealed class WindowsUiAutomation : IEasiNoteUi
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan ClickReadyTimeout = TimeSpan.FromSeconds(5);

    private AutomationElement? _window;

    /// <inheritdoc />
    public async Task<bool> WaitForElementAsync(string automationId, TimeSpan timeout, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(automationId);

        var deadline = DateTime.UtcNow + timeout;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (Find(automationId) is not null)
            {
                return true;
            }

            if (DateTime.UtcNow >= deadline)
            {
                return false;
            }

            await Task.Delay(PollInterval, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task<bool> ClickAsync(string automationId, CancellationToken cancellationToken)
    {
        // 窗口刚出现时控件可能短暂处于禁用状态（例如单点登录列表还在加载），
        // 先等它可用再点，避免把"还没加载完"当成失败。
        var deadline = DateTime.UtcNow + ClickReadyTimeout;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var element = Find(automationId);
            if (element is not null && element.Current.IsEnabled)
            {
                return TryClick(element);
            }

            if (DateTime.UtcNow >= deadline)
            {
                return element is not null && TryClick(element);
            }

            await Task.Delay(PollInterval, cancellationToken).ConfigureAwait(false);
        }
    }

    private bool TryClick(AutomationElement element)
    {
        try
        {
            if (element.TryGetCurrentPattern(InvokePattern.Pattern, out var invoke))
            {
                ((InvokePattern)invoke).Invoke();
                return true;
            }

            if (element.TryGetCurrentPattern(TogglePattern.Pattern, out var toggle))
            {
                ((TogglePattern)toggle).Toggle();
                return true;
            }

            if (element.TryGetCurrentPattern(SelectionItemPattern.Pattern, out var selection))
            {
                ((SelectionItemPattern)selection).Select();
                return true;
            }

            // 兜底：点矩形中心
            var rect = element.Current.BoundingRectangle;
            if (rect.IsEmpty || rect.Width <= 0 || rect.Height <= 0)
            {
                return false;
            }

            NativeClick.Click(
                (int)(rect.X + (rect.Width / 2)),
                (int)(rect.Y + (rect.Height / 2)));

            return true;
        }
        catch (Exception exception) when (exception is ElementNotAvailableException or ElementNotEnabledException or InvalidOperationException)
        {
            _window = null;
            return false;
        }
    }

    /// <inheritdoc />
    public Task<bool> SetTextAsync(string automationId, string text, CancellationToken cancellationToken)
    {
        var element = Find(automationId);
        if (element is null)
        {
            return Task.FromResult(false);
        }

        try
        {
            var target = element;
            if (element.Current.ControlType == ControlType.ComboBox)
            {
                var edit = element.FindFirst(
                    TreeScope.Descendants,
                    new PropertyCondition(AutomationElement.AutomationIdProperty, EasiNoteUiIds.AccountEditPart));

                if (edit is not null)
                {
                    target = edit;
                }
            }

            if (target.TryGetCurrentPattern(ValuePattern.Pattern, out var value))
            {
                ((ValuePattern)value).SetValue(text);
                return Task.FromResult(true);
            }

            target.SetFocus();
            return Task.FromResult(false);
        }
        catch (ElementNotAvailableException)
        {
            _window = null;
            return Task.FromResult(false);
        }
    }

    /// <inheritdoc />
    public Task<bool?> IsToggledAsync(string automationId, CancellationToken cancellationToken)
    {
        var element = Find(automationId);
        if (element is null)
        {
            return Task.FromResult<bool?>(null);
        }

        try
        {
            return element.TryGetCurrentPattern(TogglePattern.Pattern, out var toggle)
                ? Task.FromResult<bool?>(((TogglePattern)toggle).Current.ToggleState == ToggleState.On)
                : Task.FromResult<bool?>(null);
        }
        catch (ElementNotAvailableException)
        {
            _window = null;
            return Task.FromResult<bool?>(null);
        }
    }

    /// <inheritdoc />
    public Task<UiaRect?> GetRectAsync(string automationId, CancellationToken cancellationToken)
    {
        var element = Find(automationId);
        if (element is null)
        {
            return Task.FromResult<UiaRect?>(null);
        }

        try
        {
            var rect = element.Current.BoundingRectangle;
            if (rect.IsEmpty || rect.Width <= 0 || rect.Height <= 0)
            {
                return Task.FromResult<UiaRect?>(null);
            }

            return Task.FromResult<UiaRect?>(
                new UiaRect((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height));
        }
        catch (ElementNotAvailableException)
        {
            _window = null;
            return Task.FromResult<UiaRect?>(null);
        }
    }

    private AutomationElement? Find(string automationId)
    {
        try
        {
            var window = EnsureWindow();
            var scope = window is null ? AutomationElement.RootElement : window;
            var condition = new PropertyCondition(AutomationElement.AutomationIdProperty, automationId);

            return scope.FindFirst(TreeScope.Descendants, condition)
                   ?? (window is null
                       ? null
                       : AutomationElement.RootElement.FindFirst(TreeScope.Descendants, condition));
        }
        catch (ElementNotAvailableException)
        {
            _window = null;
            return null;
        }
    }

    private AutomationElement? EnsureWindow()
    {
        if (_window is not null)
        {
            return _window;
        }

        _window = AutomationElement.RootElement.FindFirst(
            TreeScope.Descendants,
            new PropertyCondition(AutomationElement.AutomationIdProperty, EasiNoteUiIds.LoginWindow));

        return _window;
    }

    private static class NativeClick
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extra);

        private const uint LeftDown = 0x0002;
        private const uint LeftUp = 0x0004;

        public static void Click(int x, int y)
        {
            _ = SetCursorPos(x, y);
            mouse_event(LeftDown, 0, 0, 0, UIntPtr.Zero);
            mouse_event(LeftUp, 0, 0, 0, UIntPtr.Zero);
        }
    }
}

using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Logging;
using EasiLoginer.Application.Abstractions;

namespace EasiLoginer.Infrastructure.Windows;

/// <summary>
/// 基于 Win32 的窗口服务，对应上游 <c>core/window.py</c>。
/// </summary>
/// <remarks>
/// 前台化流程逐项照抄：先判断窗口有效且可见；已是前台直接成功；
/// 最小化用 <c>SW_RESTORE</c>，否则 <c>SW_SHOW</c>；<c>SetForegroundWindow</c> 后复查，
/// 失败且允许时模拟一次 Alt 键再试（用于解除系统的前台锁定）。
/// </remarks>
public sealed class WindowsWindowService : IWindowService
{
    private const int SwShow = 5;
    private const int SwRestore = 9;
    private const byte VkMenu = 0x12;
    private const uint KeyEventKeyUp = 0x0002;

    private readonly ILogger<WindowsWindowService> _logger;

    /// <summary>创建窗口服务。</summary>
    public WindowsWindowService(ILogger<WindowsWindowService> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <inheritdoc />
    public long FindWindow(string title, bool substring = false)
    {
        ArgumentNullException.ThrowIfNull(title);

        if (!substring)
        {
            return FindWindowW(null, title);
        }

        foreach (var window in EnumerateWindows())
        {
            if (window.Title.Contains(title, StringComparison.Ordinal))
            {
                return window.Handle;
            }
        }

        return 0;
    }

    /// <inheritdoc />
    public bool SwitchToWindow(long handle, bool pressKey = false)
    {
        var hwnd = new IntPtr(handle);

        try
        {
            if (!IsWindow(hwnd) || !IsWindowVisible(hwnd))
            {
                return false;
            }

            if (GetForegroundWindowHandle() == hwnd)
            {
                return true;
            }

            _ = ShowWindow(hwnd, IsIconic(hwnd) ? SwRestore : SwShow);
            _ = BringWindowToTop(hwnd);

            // 先直接抢；失败后附加到前台线程再抢（后台进程无权直接 SetForegroundWindow）
            ForceForeground(hwnd);
            if (GetForegroundWindowHandle() == hwnd)
            {
                return true;
            }

            if (!pressKey)
            {
                return false;
            }

            // 模拟一次 Alt 键，让系统把当前线程标记为"有用户输入"，再抢一次
            keybd_event(VkMenu, 0, 0, UIntPtr.Zero);
            keybd_event(VkMenu, 0, KeyEventKeyUp, UIntPtr.Zero);
            ForceForeground(hwnd);

            return GetForegroundWindowHandle() == hwnd;
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
        {
            _logger.LogError(exception, "切换窗口焦点时发生异常");
            return false;
        }
    }

    /// <summary>
    /// 把窗口强行带到前台：临时附加到当前前台窗口的输入队列后再
    /// <c>SetForegroundWindow</c>，这是绕开后台上限的通用做法。
    /// </summary>
    private static void ForceForeground(IntPtr hwnd)
    {
        var currentThread = GetCurrentThreadId();
        var foreground = GetForegroundWindowHandle();
        var foregroundThread = foreground == IntPtr.Zero ? 0 : GetWindowThreadProcessId(foreground, out _);
        var targetThread = GetWindowThreadProcessId(hwnd, out _);

        var attachedToForeground = false;
        var attachedToTarget = false;

        try
        {
            if (foregroundThread != 0 && foregroundThread != currentThread)
            {
                attachedToForeground = AttachThreadInput(currentThread, foregroundThread, true);
            }

            if (targetThread != 0 && targetThread != currentThread && targetThread != foregroundThread)
            {
                attachedToTarget = AttachThreadInput(currentThread, targetThread, true);
            }

            _ = BringWindowToTop(hwnd);
            _ = SetForegroundWindow(hwnd);
        }
        finally
        {
            if (attachedToTarget)
            {
                _ = AttachThreadInput(currentThread, targetThread, false);
            }

            if (attachedToForeground)
            {
                _ = AttachThreadInput(currentThread, foregroundThread, false);
            }
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<WindowInfo> EnumerateWindows()
    {
        var windows = new List<WindowInfo>();

        _ = EnumWindows((hwnd, _) =>
        {
            var title = GetWindowTextOf(hwnd);
            var className = GetClassNameOf(hwnd);

            if (title.Length > 0 || className.Contains("easinote", StringComparison.OrdinalIgnoreCase))
            {
                windows.Add(new WindowInfo(hwnd.ToInt64(), title, className));
            }

            return true;
        }, IntPtr.Zero);

        return windows;
    }

    /// <inheritdoc />
    public long GetForegroundWindow() => GetForegroundWindowNative().ToInt64();

    private static IntPtr GetForegroundWindowHandle() => GetForegroundWindowNative();

    private static string GetWindowTextOf(IntPtr hwnd)
    {
        var buffer = new StringBuilder(512);
        var length = GetWindowTextW(hwnd, buffer, buffer.Capacity);
        return length > 0 ? buffer.ToString(0, length) : string.Empty;
    }

    private static string GetClassNameOf(IntPtr hwnd)
    {
        var buffer = new StringBuilder(512);
        var length = GetClassNameW(hwnd, buffer, buffer.Capacity);
        return length > 0 ? buffer.ToString(0, length) : string.Empty;
    }

    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr FindWindowW(string? className, string? windowName);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetWindowTextW(IntPtr hwnd, StringBuilder text, int maxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetClassNameW(IntPtr hwnd, StringBuilder className, int maxCount);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr hwnd, int command);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hwnd);

    [DllImport("user32.dll", EntryPoint = "GetForegroundWindow", SetLastError = true)]
    private static extern IntPtr GetForegroundWindowNative();

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool BringWindowToTop(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool attach);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern void keybd_event(byte virtualKey, byte scanCode, uint flags, UIntPtr extraInfo);
}

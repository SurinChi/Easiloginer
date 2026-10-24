using System.Runtime.InteropServices;

namespace EasiLoginer.Presentation.Login;

/// <summary>覆盖层窗口需要的少量 Win32 调用。</summary>
internal static partial class NativeMethods
{
    /// <summary>读取窗口扩展样式。</summary>
    public static int GetWindowLong(IntPtr hwnd, int index)
        => IntPtr.Size == 8 ? GetWindowLongPtr64(hwnd, index).ToInt32() : GetWindowLong32(hwnd, index);

    /// <summary>写入窗口扩展样式。</summary>
    public static int SetWindowLong(IntPtr hwnd, int index, int newStyle)
        => IntPtr.Size == 8
            ? SetWindowLongPtr64(hwnd, index, new IntPtr(newStyle)).ToInt32()
            : SetWindowLong32(hwnd, index, newStyle);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    private static extern int GetWindowLong32(IntPtr hwnd, int index);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hwnd, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static extern int SetWindowLong32(IntPtr hwnd, int index, int newStyle);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hwnd, int index, IntPtr newStyle);
}

using System.Runtime.InteropServices;

namespace EasiLoginer.Automation.Input;

/// <summary>
/// 基于 <c>SendInput</c> / <c>mouse_event</c> / 剪贴板 API 的输入后端。
/// </summary>
/// <remarks>
/// 对应上游 <c>pyautogui</c> + <c>pyperclip</c> 的能力：
/// 键盘用 <c>KEYEVENTF_UNICODE</c> 逐字符发送，鼠标用 <c>SetCursorPos</c> + <c>mouse_event</c>，
/// 密码等敏感输入在兼容模式下走剪贴板粘贴。
/// </remarks>
public sealed class WindowsInputBackend : IInputBackend
{
    private const uint InputKeyboard = 1;
    private const uint KeyEventKeyUp = 0x0002;
    private const uint KeyEventUnicode = 0x0004;
    private const uint MouseEventLeftDown = 0x0002;
    private const uint MouseEventLeftUp = 0x0004;
    private const uint CfUnicodeText = 13;
    private const uint GmemMoveable = 0x0042;

    /// <inheritdoc />
    public void Click(int x, int y)
    {
        _ = SetCursorPos(x, y);
        mouse_event(MouseEventLeftDown, 0, 0, 0, UIntPtr.Zero);
        mouse_event(MouseEventLeftUp, 0, 0, 0, UIntPtr.Zero);
    }

    /// <inheritdoc />
    public void SendUnicode(char character)
    {
        var inputs = new Input[2];

        inputs[0].Type = InputKeyboard;
        inputs[0].Data.Keyboard = new KeyboardInput
        {
            VirtualKey = 0,
            ScanCode = character,
            Flags = KeyEventUnicode,
            Time = 0,
            ExtraInfo = UIntPtr.Zero,
        };

        inputs[1].Type = InputKeyboard;
        inputs[1].Data.Keyboard = new KeyboardInput
        {
            VirtualKey = 0,
            ScanCode = character,
            Flags = KeyEventUnicode | KeyEventKeyUp,
            Time = 0,
            ExtraInfo = UIntPtr.Zero,
        };

        _ = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
    }

    /// <inheritdoc />
    public void SendKey(VirtualKey key, bool keyUp)
    {
        var inputs = new Input[1];
        inputs[0].Type = InputKeyboard;
        inputs[0].Data.Keyboard = new KeyboardInput
        {
            VirtualKey = (ushort)key,
            ScanCode = 0,
            Flags = keyUp ? KeyEventKeyUp : 0,
            Time = 0,
            ExtraInfo = UIntPtr.Zero,
        };

        _ = SendInput(1, inputs, Marshal.SizeOf<Input>());
    }

    /// <inheritdoc />
    public void SendCtrl(VirtualKey key)
    {
        SendKey(VirtualKey.Control, keyUp: false);
        SendKey(key, keyUp: false);
        SendKey(key, keyUp: true);
        SendKey(VirtualKey.Control, keyUp: true);
    }

    /// <inheritdoc />
    public bool TrySetClipboardText(string text)
    {
        if (!OpenClipboard(IntPtr.Zero))
        {
            return false;
        }

        IntPtr buffer = IntPtr.Zero;

        try
        {
            _ = EmptyClipboard();

            var bytes = (text.Length + 1) * sizeof(char);
            buffer = GlobalAlloc(GmemMoveable, (UIntPtr)bytes);
            if (buffer == IntPtr.Zero)
            {
                return false;
            }

            var pointer = GlobalLock(buffer);
            if (pointer == IntPtr.Zero)
            {
                return false;
            }

            try
            {
                Marshal.Copy(text.ToCharArray(), 0, pointer, text.Length);
                Marshal.WriteInt16(pointer, text.Length * sizeof(char), 0);
            }
            finally
            {
                _ = GlobalUnlock(buffer);
            }

            // 成功后内存归系统所有，不能再释放
            if (SetClipboardData(CfUnicodeText, buffer) == IntPtr.Zero)
            {
                return false;
            }

            buffer = IntPtr.Zero;
            return true;
        }
        finally
        {
            if (buffer != IntPtr.Zero)
            {
                _ = GlobalFree(buffer);
            }

            _ = CloseClipboard();
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputUnion Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public MouseInput Mouse;

        [FieldOffset(0)]
        public KeyboardInput Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int Dx;
        public int Dy;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extraInfo);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint count, [In] Input[] inputs, int size);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenClipboard(IntPtr owner);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EmptyClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetClipboardData(uint format, IntPtr handle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalAlloc(uint flags, UIntPtr bytes);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalLock(IntPtr handle);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalUnlock(IntPtr handle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalFree(IntPtr handle);
}

namespace EasiLoginer.Automation.Input;

/// <summary>需要用到的虚拟键码。</summary>
public enum VirtualKey
{
    /// <summary>退格。</summary>
    Back = 0x08,

    /// <summary>回车。</summary>
    Return = 0x0D,

    /// <summary>Ctrl。</summary>
    Control = 0x11,

    /// <summary>A。</summary>
    A = 0x41,

    /// <summary>V。</summary>
    V = 0x56,
}

/// <summary>
/// 原始输入后端（直接调用 Win32）。
/// </summary>
/// <remarks>
/// 抽出来是为了让输入序列（Ctrl+A → Backspace → 逐字符/剪贴板 → 回车）
/// 可以在不移动真实鼠标键盘的情况下被测试。
/// </remarks>
public interface IInputBackend
{
    /// <summary>把光标移到指定物理像素并左键单击。</summary>
    void Click(int x, int y);

    /// <summary>发送一个 Unicode 字符（<c>KEYEVENTF_UNICODE</c>）。</summary>
    void SendUnicode(char character);

    /// <summary>发送按键的按下或抬起。</summary>
    void SendKey(VirtualKey key, bool keyUp);

    /// <summary>发送 Ctrl + 指定按键的组合。</summary>
    void SendCtrl(VirtualKey key);

    /// <summary>把文本写入剪贴板；失败返回 <see langword="false"/>。</summary>
    bool TrySetClipboardText(string text);
}

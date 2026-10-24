namespace EasiLoginer.Application.Abstractions;

/// <summary>顶层窗口信息（句柄、标题、类名）。</summary>
/// <param name="Handle">窗口句柄。</param>
/// <param name="Title">窗口标题。</param>
/// <param name="ClassName">窗口类名。</param>
public readonly record struct WindowInfo(long Handle, string Title, string ClassName);

/// <summary>窗口查找与前台化端口，对应上游 <c>core/window.py</c>。</summary>
public interface IWindowService
{
    /// <summary>按标题查找窗口；未找到返回 0。</summary>
    /// <param name="title">目标标题。</param>
    /// <param name="substring">为 <see langword="true"/> 时按包含匹配（用于精确匹配失败的场景）。</param>
    long FindWindow(string title, bool substring = false);

    /// <summary>把窗口切到前台并激活；失败返回 <see langword="false"/>（调用方只记警告）。</summary>
    /// <param name="handle">窗口句柄。</param>
    /// <param name="pressKey">精确激活失败时是否模拟 Alt 键再试一次。</param>
    bool SwitchToWindow(long handle, bool pressKey = false);

    /// <summary>枚举顶层窗口：有标题的窗口，以及类名含 <c>easinote</c> 的无标题窗口。</summary>
    IReadOnlyList<WindowInfo> EnumerateWindows();

    /// <summary>当前前台窗口句柄。</summary>
    long GetForegroundWindow();
}

namespace EasiLoginer.Automation.Uia;

/// <summary>UIA 元素矩形（物理像素）。</summary>
/// <param name="X">左上角 X。</param>
/// <param name="Y">左上角 Y。</param>
/// <param name="Width">宽。</param>
/// <param name="Height">高。</param>
public readonly record struct UiaRect(int X, int Y, int Width, int Height);

/// <summary>
/// 希沃登录界面的 UI Automation 端口。
/// </summary>
/// <remarks>
/// 抽成接口的意义：策略流程（顺序、重试、失败分类）可以在没有真机的情况下单测，
/// 真实实现只负责「按 id 找元素并操作」。
/// </remarks>
public interface IEasiNoteUi
{
    /// <summary>等待元素出现。</summary>
    Task<bool> WaitForElementAsync(string automationId, TimeSpan timeout, CancellationToken cancellationToken);

    /// <summary>点击元素（Invoke / Toggle / SelectionItem，最后回退到点矩形中心）。</summary>
    Task<bool> ClickAsync(string automationId, CancellationToken cancellationToken);

    /// <summary>写入文本（ValuePattern）；元素不存在或不支持时返回 false。</summary>
    Task<bool> SetTextAsync(string automationId, string text, CancellationToken cancellationToken);

    /// <summary>读取复选框状态；元素不存在时返回 <see langword="null"/>。</summary>
    Task<bool?> IsToggledAsync(string automationId, CancellationToken cancellationToken);

    /// <summary>取元素矩形；元素不存在时返回 <see langword="null"/>。</summary>
    Task<UiaRect?> GetRectAsync(string automationId, CancellationToken cancellationToken);
}

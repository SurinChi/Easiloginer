namespace EasiLoginer.Automation.Input;

/// <summary>输入模拟端口：点击、输入文本、回车。</summary>
/// <remarks>
/// 实现位于同项目（<c>SendInput</c> 封装 + 兼容模式剪贴板）；
/// 策略只依赖本接口，便于用假实现验证点击序列与坐标。
/// </remarks>
public interface IInputSimulator
{
    /// <summary>在物理像素坐标处点击左键。</summary>
    Task ClickAsync(int x, int y, CancellationToken cancellationToken);

    /// <summary>输入文本；<paramref name="secret"/> 为真时日志不得出现明文。</summary>
    Task TypeTextAsync(string text, bool secret, CancellationToken cancellationToken);

    /// <summary>按下回车。</summary>
    Task PressEnterAsync(CancellationToken cancellationToken);
}

using Microsoft.Extensions.Logging;
using EasiLoginer.Domain.Geometry;

namespace EasiLoginer.Automation.Input;

/// <summary>
/// 输入模拟实现，对应上游 <c>pyautogui_base.py</c>。
/// </summary>
/// <remarks>
/// <para>时序常量与上游一致：每步 100 ms 停顿、逐字符 10 ms 间隔
/// （pyautogui 的 <c>PAUSE=0.1</c> 与 <c>interval=0.01</c>）。</para>
/// <para>兼容模式：物理高度除以缩放小于 720，或用户强制开启时，
/// 输入改为"写剪贴板 + Ctrl+V"，避免输入法遮挡等问题。</para>
/// <para>每次输入前都会 Ctrl+A + Backspace 清空目标框（与上游 <c>clear=True</c> 一致）。</para>
/// </remarks>
public sealed class SendInputSimulator : IInputSimulator
{
    /// <summary>每步停顿，默认 100 ms。</summary>
    public static readonly TimeSpan DefaultStepPause = TimeSpan.FromMilliseconds(100);

    /// <summary>逐字符间隔，默认 10 ms。</summary>
    public static readonly TimeSpan DefaultCharacterInterval = TimeSpan.FromMilliseconds(10);

    private readonly IInputBackend _backend;
    private readonly ILogger<SendInputSimulator> _logger;
    private readonly TimeSpan _stepPause;
    private readonly TimeSpan _characterInterval;

    /// <summary>创建输入模拟器。</summary>
    /// <param name="backend">原始输入后端。</param>
    /// <param name="screen">屏幕度量（用于判定兼容模式）。</param>
    /// <param name="forceCompatibilityMode">用户强制兼容模式。</param>
    /// <param name="logger">日志。</param>
    /// <param name="stepPause">每步停顿，默认 100 ms。</param>
    /// <param name="characterInterval">逐字符间隔，默认 10 ms。</param>
    public SendInputSimulator(
        IInputBackend backend,
        ScreenMetrics screen,
        bool forceCompatibilityMode,
        ILogger<SendInputSimulator> logger,
        TimeSpan? stepPause = null,
        TimeSpan? characterInterval = null)
    {
        ArgumentNullException.ThrowIfNull(backend);
        ArgumentNullException.ThrowIfNull(screen);
        ArgumentNullException.ThrowIfNull(logger);

        _backend = backend;
        _logger = logger;
        _stepPause = stepPause ?? DefaultStepPause;
        _characterInterval = characterInterval ?? DefaultCharacterInterval;

        CompatibilityMode = forceCompatibilityMode || (screen.PhysicalSize.Y / screen.Scale) < 720;

        if (CompatibilityMode)
        {
            _logger.LogInformation("输入将使用兼容模式（剪贴板粘贴）");
        }
    }

    /// <summary>是否启用兼容模式输入。</summary>
    public bool CompatibilityMode { get; }

    /// <inheritdoc />
    public async Task ClickAsync(int x, int y, CancellationToken cancellationToken)
    {
        _logger.LogDebug("点击：({X}, {Y})", x, y);
        _backend.Click(x, y);
        await PauseAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task TypeTextAsync(string text, bool secret, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(text);

        _logger.LogDebug("输入：{Text}", secret ? Mask(text) : text);

        // 先清空目标框，避免与既有内容拼接
        _backend.SendCtrl(VirtualKey.A);
        _backend.SendKey(VirtualKey.Back, keyUp: false);
        _backend.SendKey(VirtualKey.Back, keyUp: true);

        if (CompatibilityMode)
        {
            if (!_backend.TrySetClipboardText(text))
            {
                throw new InvalidOperationException("写入剪贴板失败，无法以兼容模式输入。");
            }

            _backend.SendCtrl(VirtualKey.V);
        }
        else
        {
            foreach (var character in text)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _backend.SendUnicode(character);

                if (_characterInterval > TimeSpan.Zero)
                {
                    await Task.Delay(_characterInterval, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        await PauseAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task PressEnterAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("按下回车");
        _backend.SendKey(VirtualKey.Return, keyUp: false);
        _backend.SendKey(VirtualKey.Return, keyUp: true);
        await PauseAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task PauseAsync(CancellationToken cancellationToken)
    {
        if (_stepPause > TimeSpan.Zero)
        {
            await Task.Delay(_stepPause, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>密码日志脱敏：长度大于 2 时保留首尾，否则全星号。</summary>
    private static string Mask(string text)
        => text.Length > 2
            ? text[0] + new string('*', text.Length - 2) + text[^1]
            : new string('*', text.Length);
}

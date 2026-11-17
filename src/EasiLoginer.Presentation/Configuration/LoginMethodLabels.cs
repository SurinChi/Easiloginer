using EasiLoginer.Domain.Configuration;

namespace EasiLoginer.Presentation.Configuration;

/// <summary>
/// 四种登录方式在界面上的统一名称，顺序与 <see cref="LoginMethod"/> 枚举一致。
/// </summary>
/// <remarks>
/// 设置页、首次向导和关于页都从这里取名称，避免同一方式出现
/// 「UI Automation」「自动定位」等多个叫法。底层实现仍然使用 UI Automation。
/// </remarks>
public static class LoginMethodLabels
{
    /// <summary>固定位置 / 图像识别 / 自动定位 / 令牌投递。</summary>
    public static string[] All { get; } = ["固定位置", "图像识别", "自动定位", "令牌投递"];
}

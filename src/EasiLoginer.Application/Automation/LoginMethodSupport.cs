using EasiLoginer.Domain.Configuration;

namespace EasiLoginer.Application.Automation;

/// <summary>
/// 登录方式的能力声明：哪些已经实现、哪些只是保留了枚举值。
/// </summary>
/// <remarks>
/// 图像识别（<see cref="LoginMethod.Cv"/>）依赖 <c>resources/EasiNoteUI/*.png</c> 界面模板，
/// 仓库里没有这些图片，因此实现出来也无法验证。这里<b>明确拒绝</b>而不是静默回退到别的策略，
/// 避免用户以为自己在用图像识别、实际跑的是固定位置。
/// </remarks>
public static class LoginMethodSupport
{
    /// <summary>该登录方式是否有可用实现。</summary>
    public static bool IsImplemented(LoginMethod method) => method != LoginMethod.Cv;

    /// <summary>不支持时抛出带解释的异常。</summary>
    public static void EnsureImplemented(LoginMethod method)
    {
        if (IsImplemented(method))
        {
            return;
        }

        throw new NotSupportedException(
            "图像识别登录（Cv）尚未实现：仓库缺少 resources/EasiNoteUI 下的界面模板，" +
            "请改用「固定位置」或「自动定位（UIA）」或「令牌投递」。");
    }
}

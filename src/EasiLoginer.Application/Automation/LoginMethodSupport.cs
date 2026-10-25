using EasiLoginer.Domain.Configuration;

namespace EasiLoginer.Application.Automation;

/// <summary>
/// 登录方式的能力声明：哪些已经有实现。
/// </summary>
/// <remarks>
/// 四种方式（固定位置 / 图像识别 / 自动定位 / 令牌投递）均已实现。
/// 这里保留一层显式校验：如果配置里出现未知枚举值（例如手工改坏 json），
/// 直接给出可读错误，而不是让流程莫名走到默认分支。
/// </remarks>
public static class LoginMethodSupport
{
    /// <summary>该登录方式是否有可用实现。</summary>
    public static bool IsImplemented(LoginMethod method) => method switch
    {
        LoginMethod.Fixed or LoginMethod.Cv or LoginMethod.Uia or LoginMethod.Token => true,
        _ => false,
    };

    /// <summary>不支持时抛出带解释的异常。</summary>
    public static void EnsureImplemented(LoginMethod method)
    {
        if (IsImplemented(method))
        {
            return;
        }

        throw new NotSupportedException($"未知的登录方式：{method}（配置可能被手工改坏）");
    }
}

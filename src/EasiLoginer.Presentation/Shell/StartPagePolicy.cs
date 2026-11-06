using EasiLoginer.Domain.Configuration;

namespace EasiLoginer.Presentation.Shell;

/// <summary>启动时默认打开哪一页。</summary>
public static class StartPagePolicy
{
    /// <summary>
    /// 按用户设置选择启动页；<see cref="StartPageOptions.Auto"/> 时有档案进快捷登录，
    /// 没有档案进档案页（方便新建）。
    /// </summary>
    /// <param name="option">配置中的启动页面。</param>
    /// <param name="profileCount">当前档案数量。</param>
    public static Type Resolve(StartPageOptions option, int profileCount)
        => option switch
        {
            StartPageOptions.QuickLogin => typeof(Pages.QuickLoginPage),
            StartPageOptions.Profile => typeof(Pages.ProfilePage),
            StartPageOptions.Config => typeof(Pages.ConfigPage),
            _ => profileCount > 0 ? typeof(Pages.QuickLoginPage) : typeof(Pages.ProfilePage),
        };

    /// <summary>兼容旧调用：按档案数量走自动规则。</summary>
    public static Type Resolve(int profileCount)
        => Resolve(StartPageOptions.Auto, profileCount);
}

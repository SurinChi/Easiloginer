namespace EasiLoginer.Presentation.Shell;

/// <summary>启动时默认打开哪一页。</summary>
public static class StartPagePolicy
{
    /// <summary>
    /// 有档案时进入快捷登录（一键登录），没有档案时进入档案页（方便新建）。
    /// </summary>
    /// <param name="profileCount">当前档案数量。</param>
    public static Type Resolve(int profileCount)
        => profileCount > 0 ? typeof(Pages.QuickLoginPage) : typeof(Pages.ProfilePage);
}

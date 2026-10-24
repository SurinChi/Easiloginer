namespace EasiLoginer.Host.CommandLine;

/// <summary>命令行子命令。</summary>
internal enum AppCommand
{
    /// <summary>无参数或 <c>settings</c>：打开主界面。</summary>
    Settings,

    /// <summary><c>login</c>：手动执行一次登录。</summary>
    Login,

    /// <summary><c>oobe</c>：重新走首次向导。</summary>
    Oobe,

    /// <summary><c>skip</c>：写一次 SkipOnce 后退出。</summary>
    Skip,

    /// <summary><c>patch</c>：修补/撤销希沃 DLL。</summary>
    Patch,
}

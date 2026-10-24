using EasiLoginer.Domain.Automation;
using EasiLoginer.Domain.Configuration;
using EasiLoginer.Domain.Geometry;

namespace EasiLoginer.Application.Automation;

/// <summary>登录过程中的 UI 回调（M5 由 WPF 呈现层实现；无 UI 时用空实现）。</summary>
public interface ILoginPresenter
{
    /// <summary>当前任务变化（如"正在准备登录"）。</summary>
    void OnTaskChanged(string task);

    /// <summary>进度文案变化（如"输入密码"）。</summary>
    void OnProgressChanged(string progress);

    /// <summary>显示隐私遮罩（物理像素）。</summary>
    void ShowPrivacyMask(int x, int y, int width, int height);

    /// <summary>隐藏隐私遮罩（幂等）。</summary>
    void HidePrivacyMask();

    /// <summary>显示警示横幅（配置启用时由管线调用）。</summary>
    void ShowBanner();

    /// <summary>隐藏警示横幅（幂等）。</summary>
    void HideBanner();

    /// <summary>流程结束。</summary>
    void OnFinished(LoginOutcome outcome, string? error);
}

/// <summary>无呈现层的空实现（命令行与测试用）。</summary>
public sealed class NullLoginPresenter : ILoginPresenter
{
    /// <summary>共享实例。</summary>
    public static NullLoginPresenter Instance { get; } = new();

    /// <inheritdoc />
    public void OnTaskChanged(string task)
    {
    }

    /// <inheritdoc />
    public void OnProgressChanged(string progress)
    {
    }

    /// <inheritdoc />
    public void ShowPrivacyMask(int x, int y, int width, int height)
    {
    }

    /// <inheritdoc />
    public void HidePrivacyMask()
    {
    }

    /// <inheritdoc />
    public void ShowBanner()
    {
    }

    /// <inheritdoc />
    public void HideBanner()
    {
    }

    /// <inheritdoc />
    public void OnFinished(LoginOutcome outcome, string? error)
    {
    }
}

/// <summary>一次登录运行的上下文：账号、配置快照与运行期状态。</summary>
public sealed class LoginContext
{
    private readonly HashSet<string> _seenProgress = new(StringComparer.Ordinal);

    /// <summary>创建上下文。</summary>
    public LoginContext(string account, string password, Config config, ILoginPresenter presenter)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(password);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(presenter);

        Account = account;
        Password = password;
        Config = config;
        Presenter = presenter;
    }

    /// <summary>账号。</summary>
    public string Account { get; }

    /// <summary>密码（仅在内存中短暂存在，不写日志）。</summary>
    public string Password { get; }

    /// <summary>配置。</summary>
    public Config Config { get; }

    /// <summary>UI 回调。</summary>
    public ILoginPresenter Presenter { get; }

    /// <summary>希沃启动器路径（prepare 阶段写入）。</summary>
    public string? EasiNotePath { get; set; }

    /// <summary>主窗口句柄（prepare 阶段写入）。</summary>
    public long WindowHandle { get; set; }

    /// <summary>本次是否进入白板界面（prepare 阶段写入）。</summary>
    public bool IsIwb { get; set; }

    /// <summary>本次实际使用的启动参数。</summary>
    public string LaunchArgs { get; set; } = string.Empty;

    /// <summary>解析出的屏幕度量快照（登录开始时解析一次）。</summary>
    public ScreenMetrics? Screen { get; set; }

    /// <summary>记录任务文案（相同文本不重复上报）。</summary>
    public void ReportTask(string task) => Presenter.OnTaskChanged(task);

    /// <summary>记录进度文案（相同文本不重复上报，与上游一致）。</summary>
    public void ReportProgress(string progress)
    {
        if (!_seenProgress.Add(progress))
        {
            return;
        }

        Presenter.OnProgressChanged(progress);
    }
}

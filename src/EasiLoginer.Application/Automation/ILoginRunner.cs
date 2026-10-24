using EasiLoginer.Application.Automation;
using EasiLoginer.Domain.Automation;

namespace EasiLoginer.Application.Automation;

/// <summary>一次 UI 登录的运行参数。</summary>
/// <param name="Presenter">进度与覆盖层回调。</param>
/// <param name="Confirmation">运行前确认；<see langword="null"/> 表示无界面环境。</param>
public sealed record LoginRunOptions(ILoginPresenter Presenter, ILoginConfirmation? Confirmation = null);

/// <summary>按档案执行一次手动登录（由组合根实现，UI 与 CLI 共用）。</summary>
public interface ILoginRunner
{
    /// <summary>用指定档案登录。</summary>
    Task<LoginOutcome> RunForProfileAsync(
        string profileId,
        LoginRunOptions options,
        CancellationToken cancellationToken);
}

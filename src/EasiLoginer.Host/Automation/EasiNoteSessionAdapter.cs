using EasiLoginer.Application.Automation;
using EasiLoginer.Domain.Automation;
using EasiLoginer.Domain.Configuration;
using EasiLoginer.Integrations.EasiNote.Environment;
using EasiLoginer.Integrations.EasiNote.Path;
using EasiLoginer.Integrations.EasiNote.Patching;
using EasiLoginer.Integrations.EasiNote.Pipe;
using EasiLoginer.Integrations.EasiNote.Process;

namespace EasiLoginer.Host.Automation;

/// <summary>
/// 把集成层能力适配成应用层端口 <see cref="IEasiNoteSession"/>。
/// </summary>
/// <remarks>组合根职责：应用层不直接依赖 Integrations（文档 §4.3 规则 4）。</remarks>
internal sealed class EasiNoteSessionAdapter : IEasiNoteSession
{
    private readonly EasiNoteLocator _locator;
    private readonly IEasiNoteProcessController _controller;
    private readonly IEasiNotePatcher _patcher;
    private readonly SeewoPipeClient _pipe;
    private readonly IwbDetector _detector;
    private readonly Func<EasiNoteConfig> _config;

    public EasiNoteSessionAdapter(
        EasiNoteLocator locator,
        IEasiNoteProcessController controller,
        IEasiNotePatcher patcher,
        SeewoPipeClient pipe,
        IwbDetector detector,
        Func<EasiNoteConfig> config)
    {
        _locator = locator;
        _controller = controller;
        _patcher = patcher;
        _pipe = pipe;
        _detector = detector;
        _config = config;
    }

    public EasiNotePathResult ResolvePath()
    {
        var location = _locator.Resolve(_config());
        return new EasiNotePathResult(location.Path, location.Source.ToString().ToLowerInvariant());
    }

    public bool IsPatched()
    {
        var path = _locator.Resolve(_config()).Path;
        return path is not null && _patcher.IsPatched(path);
    }

    public bool ResolveIsIwb(string args) => _detector.ResolveIsIwb(args);

    public void KillTargets(string processName, bool killAgent, string extraKills, double timeoutSeconds)
        => _controller.KillTargets(processName, killAgent, extraKills, timeoutSeconds);

    public void Launch(string executablePath, string args) => _controller.Launch(executablePath, args);

    public Task<long> WaitForWindowAsync(string title, bool substring, TimeSpan timeout, TimeSpan interval, CancellationToken cancellationToken)
        => _controller.WaitForWindowAsync(title, substring, timeout, interval, cancellationToken);

    public bool BringToFront(long handle) => _controller.BringToFront(handle);

    public async Task<EasiNoteLoginState?> ReadLoginStateAsync(CancellationToken cancellationToken)
    {
        var info = await _pipe.ReadCurrentLoginInfoAsync(addToLoggedTokens: false, cancellationToken).ConfigureAwait(false);
        return info is null ? null : new EasiNoteLoginState(info.StatusCode, info.UserId, info.Token);
    }
}

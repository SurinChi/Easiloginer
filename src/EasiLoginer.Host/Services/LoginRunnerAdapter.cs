using EasiLoginer.Application.Automation;
using EasiLoginer.Domain.Automation;
using EasiLoginer.Host.Automation;

namespace EasiLoginer.Host.Services;

/// <summary>把 <see cref="LoginRunner"/> 适配成 UI 使用的 <see cref="ILoginRunner"/>。</summary>
internal sealed class LoginRunnerAdapter : ILoginRunner
{
    /// <inheritdoc />
    public Task<LoginOutcome> RunForProfileAsync(
        string profileId,
        LoginRunOptions options,
        CancellationToken cancellationToken)
        => LoginRunner.RunProfileAsync(profileId, options, cancellationToken);
}

using EasiLoginer.Application.Update;
using EasiLoginer.Domain.Configuration;

namespace EasiLoginer.Presentation.Update;

/// <summary>
/// 未配置更新源时的占位实现：始终返回 <see langword="null"/>，让检查器给出
/// 「尚未配置更新源」的结论，而不是伪造一个更新地址。
/// </summary>
/// <remarks>
/// 接入自有清单后，把这里换成读取配置中地址的 HTTP 实现即可，界面无需改动。
/// </remarks>
public sealed class NotConfiguredUpdateSource : IUpdateSource
{
    /// <inheritdoc />
    public Task<string?> FetchManifestJsonAsync(UpdateChannel channel, CancellationToken cancellationToken)
        => Task.FromResult<string?>(null);
}

using EasiLoginer.Application.Abstractions;
using EasiLoginer.Domain.Configuration;
using EasiLoginer.Infrastructure.Configuration;

namespace EasiLoginer.Host.Services;

/// <summary>把配置存储适配成 <see cref="IConfigService"/>。</summary>
internal sealed class ConfigServiceAdapter(ConfigStore store, Config config) : IConfigService
{
    /// <inheritdoc />
    public Config Current => config;

    /// <inheritdoc />
    public void Save() => store.Save(config);
}

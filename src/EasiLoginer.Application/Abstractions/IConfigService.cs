using EasiLoginer.Domain.Configuration;

namespace EasiLoginer.Application.Abstractions;

/// <summary>配置读写端口（呈现层不直接依赖基础设施的 JSON 存储）。</summary>
public interface IConfigService
{
    /// <summary>当前配置（内存中的单一事实源）。</summary>
    Config Current { get; }

    /// <summary>把当前配置原子写入磁盘。</summary>
    void Save();
}

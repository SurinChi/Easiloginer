namespace EasiLoginer.Infrastructure.Configuration;

/// <summary>配置持久化后端；默认使用注册表，测试与迁移可继续使用文件。</summary>
public interface IConfigBackend
{
    /// <summary>后端是否已有配置。</summary>
    bool Exists { get; }

    /// <summary>读取原始 JSON；读取失败时抛出 IO 相关异常。</summary>
    string Read();

    /// <summary>写入完整 JSON。</summary>
    void Write(string json);

    /// <summary>配置损坏时尽量保留一份原始副本。</summary>
    void BackupCorrupt();
}

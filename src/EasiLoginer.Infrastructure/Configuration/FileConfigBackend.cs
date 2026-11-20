using System.Text;
using EasiLoginer.Infrastructure.Paths;
using EasiLoginer.Infrastructure.Storage;

namespace EasiLoginer.Infrastructure.Configuration;

/// <summary><c>config.json</c> 后端：用于迁移与自动化测试。</summary>
public sealed class FileConfigBackend : IConfigBackend
{
    private readonly AppPaths _paths;

    /// <summary>创建文件后端。</summary>
    public FileConfigBackend(AppPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        _paths = paths;
    }

    /// <inheritdoc />
    public bool Exists => File.Exists(_paths.ConfigFile);

    /// <inheritdoc />
    public string Read() => File.ReadAllText(_paths.ConfigFile, Encoding.UTF8);

    /// <inheritdoc />
    public void Write(string json)
    {
        Directory.CreateDirectory(_paths.DataDirectory);
        AtomicFile.WriteAllText(_paths.ConfigFile, json);
    }

    /// <inheritdoc />
    public void BackupCorrupt()
    {
        File.Copy(_paths.ConfigFile, _paths.ConfigFile + ".bak", overwrite: true);
    }
}

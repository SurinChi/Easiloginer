using EasiLoginer.Infrastructure.Paths;
using EasiLoginer.Infrastructure.Storage;
using FluentAssertions;

namespace EasiLoginer.Infrastructure.Tests;

/// <summary>启动状态与数据清理（长期未启动时恢复初始状态）。</summary>
public sealed class DataRetentionStorageTests : IDisposable
{
    private readonly string _root;
    private readonly AppPaths _paths;

    public DataRetentionStorageTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "EasiLoginerTests", "retention", Guid.NewGuid().ToString("N"));
        _paths = new AppPaths(_root);
        _paths.EnsureWritableDirectories();
    }

    /// <summary>启动时间可写入并读回。</summary>
    [Fact]
    public void 状态往返()
    {
        var store = new LaunchStateStore(_paths);
        var timestamp = new DateTimeOffset(2026, 10, 30, 12, 0, 0, TimeSpan.Zero);

        store.Save(new LaunchState(timestamp));
        var loaded = store.Load();

        loaded.LastLaunchUtc.Should().Be(timestamp);
    }

    /// <summary>文件缺失或损坏时返回空状态，不抛异常。</summary>
    [Fact]
    public void 缺失或损坏返回空()
    {
        var store = new LaunchStateStore(_paths);
        store.Load().LastLaunchUtc.Should().BeNull();

        File.WriteAllText(store.FilePath, "{ not json");
        store.Load().LastLaunchUtc.Should().BeNull();
    }

    /// <summary>清理会删除数据目录下的全部内容，但保留目录本身。</summary>
    [Fact]
    public void 清理数据目录()
    {
        File.WriteAllText(Path.Combine(_paths.DataDirectory, "config.json"), "{}");
        File.WriteAllText(Path.Combine(_paths.DataDirectory, "profile.json"), "{}");
        Directory.CreateDirectory(Path.Combine(_paths.DataDirectory, "cache"));
        File.WriteAllText(Path.Combine(_paths.DataDirectory, "cache", "blob.bin"), "x");

        var removed = DataWipeService.Wipe(_paths);

        removed.Should().Be(3);
        Directory.Exists(_paths.DataDirectory).Should().BeTrue("目录本身要留给后续写入");
        Directory.EnumerateFiles(_paths.DataDirectory, "*", SearchOption.AllDirectories)
            .Should().BeEmpty();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch (IOException)
        {
        }
    }
}

using EasiLoginer.Domain.Configuration;
using EasiLoginer.Integrations.EasiNote.Path;
using FluentAssertions;

namespace EasiLoginer.Integrations.Tests;

/// <summary>希沃启动器路径解析（文档 §8.1）。</summary>
public sealed class EasiNoteLocatorTests : IDisposable
{
    private readonly string _root;

    public EasiNoteLocatorTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "EasiLoginerTests", "locator", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    /// <summary>注册表有值且文件存在：来源为 registry。</summary>
    [Fact]
    public void 注册表命中()
    {
        var exe = CreateFile("swenlauncher.exe");
        var locator = new EasiNoteLocator(new FakeRegistry(exe));

        var result = locator.Resolve(new EasiNoteConfig { AutoPath = true });

        result.Path.Should().Be(exe);
        result.Source.Should().Be(EasiNotePathSource.Registry);
    }

    /// <summary>注册表无值：回退到默认路径，来源为 fallback（默认路径不存在时 Path 为 null）。</summary>
    [Fact]
    public void 注册表失败时回退()
    {
        var locator = new EasiNoteLocator(new FakeRegistry(null));

        var result = locator.Resolve(new EasiNoteConfig { AutoPath = true });

        result.Source.Should().Be(EasiNotePathSource.Fallback);
        if (!File.Exists(EasiNoteLocator.FallbackPath))
        {
            result.Path.Should().BeNull();
        }
    }

    /// <summary>手动路径：来源为 manual。</summary>
    [Fact]
    public void 手动路径()
    {
        var exe = CreateFile("manual.exe");
        var locator = new EasiNoteLocator(new FakeRegistry(@"C:\should-not-be-used.exe"));

        var result = locator.Resolve(new EasiNoteConfig { AutoPath = false, Path = exe });

        result.Path.Should().Be(exe);
        result.Source.Should().Be(EasiNotePathSource.Manual);
    }

    /// <summary>路径不存在时返回 null，但保留来源信息。</summary>
    [Fact]
    public void 路径不存在时返回空()
    {
        var missing = Path.Combine(_root, "missing.exe");
        var locator = new EasiNoteLocator(new FakeRegistry(null));

        var result = locator.Resolve(new EasiNoteConfig { AutoPath = false, Path = missing });

        result.Path.Should().BeNull();
        result.Source.Should().Be(EasiNotePathSource.Manual);
    }

    /// <summary>非法路径不抛异常。</summary>
    [Fact]
    public void 非法路径不抛异常()
    {
        var locator = new EasiNoteLocator(new FakeRegistry(null));

        var result = locator.Resolve(new EasiNoteConfig { AutoPath = false, Path = "::::" });

        result.Path.Should().BeNull();
    }

    private string CreateFile(string name)
    {
        var path = Path.Combine(_root, name);
        File.WriteAllText(path, string.Empty);
        return path;
    }

    private sealed class FakeRegistry(string? value) : IRegistryReader
    {
        public string? ReadLocalMachineString(string subKey, string valueName) => value;
    }
}

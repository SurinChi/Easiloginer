using EasiLoginer.Infrastructure.Paths;
using FluentAssertions;

namespace EasiLoginer.Infrastructure.Tests;

/// <summary>便携目录布局（文档 §7.1）。</summary>
public sealed class AppPathsTests
{
    private static readonly string BaseDirectory = Path.Combine(Path.GetTempPath(), "EasiLoginerTests", "root");

    /// <summary>所有可变数据都落在 exe 同级 data/ 下，不写 %APPDATA%。</summary>
    [Fact]
    public void 数据目录位于应用根目录下()
    {
        var paths = new AppPaths(BaseDirectory);

        paths.DataDirectory.Should().Be(Path.Combine(BaseDirectory, "data"));
        paths.ConfigFile.Should().Be(Path.Combine(BaseDirectory, "data", "config.json"));
        paths.ProfileFile.Should().Be(Path.Combine(BaseDirectory, "data", "profile.json"));
        paths.UnlockFile.Should().Be(Path.Combine(BaseDirectory, "data", "profile.unlock"));
        paths.LogsDirectory.Should().Be(Path.Combine(BaseDirectory, "data", "logs"));
    }

    /// <summary>资源与第三方二进制是只读的，位于 exe 同级。</summary>
    [Fact]
    public void 资源目录位于应用根目录下()
    {
        var paths = new AppPaths(BaseDirectory);

        paths.ResourcesDirectory.Should().Be(Path.Combine(BaseDirectory, "resources"));
        paths.VendorsDirectory.Should().Be(Path.Combine(BaseDirectory, "vendors"));
    }

    /// <summary>相对路径无法确定 exe 位置，必须拒绝。</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("relative")]
    [InlineData(@"relative\data")]
    public void 相对路径被拒绝(string baseDirectory)
    {
        var action = () => _ = new AppPaths(baseDirectory);

        action.Should().Throw<ArgumentException>();
    }

    /// <summary>目录创建是幂等的。</summary>
    [Fact]
    public void 创建目录幂等()
    {
        var root = Path.Combine(Path.GetTempPath(), "EasiLoginerTests", Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new AppPaths(root);

            paths.EnsureWritableDirectories();
            paths.EnsureWritableDirectories();

            Directory.Exists(paths.DataDirectory).Should().BeTrue();
            Directory.Exists(paths.LogsDirectory).Should().BeTrue();
            Directory.Exists(paths.CacheDirectory).Should().BeTrue();
            Directory.Exists(paths.AvatarsDirectory).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}

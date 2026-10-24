using EasiLoginer.Application.Update;
using EasiLoginer.Domain.Configuration;
using EasiLoginer.Domain.Update;
using EasiLoginer.Domain.Versioning;
using FluentAssertions;

namespace EasiLoginer.Application.Tests;

/// <summary>更新检查：清单解析、版本比较与通道校验（文档 §11.1）。</summary>
public sealed class UpdateCheckerTests
{
    /// <summary>未配置更新源。</summary>
    [Fact]
    public async Task 未配置更新源()
    {
        var result = await CheckAsync(new FakeSource(null), "0.0.1", UpdateChannel.Release);

        result.Availability.Should().Be(UpdateAvailability.NotConfigured);
        result.Manifest.Should().BeNull();
    }

    /// <summary>空清单与未配置等价。</summary>
    [Fact]
    public async Task 空清单按未配置处理()
    {
        var result = await CheckAsync(new FakeSource("   "), "0.0.1", UpdateChannel.Release);

        result.Availability.Should().Be(UpdateAvailability.NotConfigured);
    }

    /// <summary>清单不是合法 JSON。</summary>
    [Fact]
    public async Task 清单格式非法()
    {
        var result = await CheckAsync(new FakeSource("{ not json"), "0.0.1", UpdateChannel.Release);

        result.Availability.Should().Be(UpdateAvailability.Failed);
    }

    /// <summary>缺少 version 字段。</summary>
    [Fact]
    public async Task 缺少版本字段()
    {
        var result = await CheckAsync(new FakeSource("""{ "channel": "release" }"""), "0.0.1", UpdateChannel.Release);

        result.Availability.Should().Be(UpdateAvailability.Failed);
    }

    /// <summary>同版本视为已是最新。</summary>
    [Fact]
    public async Task 同版本最新()
    {
        var result = await CheckAsync(new FakeSource(Manifest("0.0.1")), "0.0.1", UpdateChannel.Release);

        result.Availability.Should().Be(UpdateAvailability.UpToDate);
    }

    /// <summary>更高版本可用，并解析出资产。</summary>
    [Fact]
    public async Task 更高版本可用()
    {
        var result = await CheckAsync(new FakeSource(Manifest("0.0.2")), "0.0.1", UpdateChannel.Release);

        result.Availability.Should().Be(UpdateAvailability.Available);
        result.Manifest.Should().NotBeNull();
        result.Manifest!.Version.ToString().Should().Be("0.0.2");
        result.Manifest.Assets.Should().HaveCount(1);
        result.Manifest.Assets[0].Sha256.Should().Be("abc123");
        result.Manifest.Notes.Should().Be("修复若干问题");
    }

    /// <summary>更低版本不会降级。</summary>
    [Fact]
    public async Task 更低版本不降级()
    {
        var result = await CheckAsync(new FakeSource(Manifest("0.0.0")), "0.0.1", UpdateChannel.Release);

        result.Availability.Should().Be(UpdateAvailability.UpToDate);
    }

    /// <summary>预发布号按 PEP 440 排序：0.0.2b1 高于 0.0.1。</summary>
    [Fact]
    public async Task 预发布号可比较()
    {
        var result = await CheckAsync(new FakeSource(Manifest("0.0.2b1")), "0.0.1", UpdateChannel.Release);

        result.Availability.Should().Be(UpdateAvailability.Available);
    }

    /// <summary>通道不一致时拒绝。</summary>
    [Fact]
    public async Task 通道不一致()
    {
        var result = await CheckAsync(new FakeSource(Manifest("0.0.2")), "0.0.1", UpdateChannel.Dev);

        result.Availability.Should().Be(UpdateAvailability.Unsupported);
    }

    /// <summary>网络异常归类为失败而不是崩溃。</summary>
    [Fact]
    public async Task 网络异常()
    {
        var result = await CheckAsync(new ThrowingSource(), "0.0.1", UpdateChannel.Release);

        result.Availability.Should().Be(UpdateAvailability.Failed);
        result.Message.Should().Contain("更新源不可用");
    }

    private static Task<UpdateCheckResult> CheckAsync(IUpdateSource source, string current, UpdateChannel channel)
        => new UpdateChecker(source).CheckAsync(Pep440Version.Parse(current), channel, CancellationToken.None);

    private static string Manifest(string version) => $$"""
        {
          "version": "{{version}}",
          "channel": "release",
          "notes": "修复若干问题",
          "assets": [
            { "source": "github", "url": "https://example.invalid/EasiLoginer.zip", "sha256": "abc123", "size": 1024 }
          ]
        }
        """;

    private sealed class FakeSource(string? json) : IUpdateSource
    {
        public Task<string?> FetchManifestJsonAsync(UpdateChannel channel, CancellationToken cancellationToken)
            => Task.FromResult(json);
    }

    private sealed class ThrowingSource : IUpdateSource
    {
        public Task<string?> FetchManifestJsonAsync(UpdateChannel channel, CancellationToken cancellationToken)
            => throw new IOException("connection refused");
    }
}

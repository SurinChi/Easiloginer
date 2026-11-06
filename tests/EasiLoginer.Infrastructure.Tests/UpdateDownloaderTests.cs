using System.Net;
using System.Security.Cryptography;
using System.Text;
using EasiLoginer.Application.Update;
using EasiLoginer.Domain.Update;
using EasiLoginer.Infrastructure.Update;
using FluentAssertions;

namespace EasiLoginer.Infrastructure.Tests;

/// <summary>更新包下载与校验。</summary>
public sealed class UpdateDownloaderTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "EasiLoginerTests", "updates", Guid.NewGuid().ToString("N"));

    /// <summary>下载成功：落盘、长度与 SHA-256 校验通过。</summary>
    [Fact]
    public async Task 下载并校验成功()
    {
        var bytes = Encoding.UTF8.GetBytes("EasiLoginer-installer-payload");
        var digest = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var asset = new UpdateAsset("github", "https://example.invalid/EasiLoginer-Setup-0.1.2.exe", digest, bytes.Length);

        var progress = new List<UpdateDownloadProgress>();
        var downloader = Create(bytes);

        var result = await downloader.DownloadAsync(
            asset,
            new Progress<UpdateDownloadProgress>(progress.Add),
            CancellationToken.None);

        File.Exists(result.FilePath).Should().BeTrue();
        (await File.ReadAllBytesAsync(result.FilePath)).Should().Equal(bytes);
        result.Size.Should().Be(bytes.Length);
        Path.GetFileName(result.FilePath).Should().Be("EasiLoginer-Setup-0.1.2.exe");
    }

    /// <summary>摘要不一致时抛出 IO 异常并清理临时文件。</summary>
    [Fact]
    public async Task 摘要不一致拒绝()
    {
        var bytes = Encoding.UTF8.GetBytes("tampered");
        var asset = new UpdateAsset("github", "https://example.invalid/setup.exe", new string('0', 64), bytes.Length);

        var downloader = Create(bytes);

        var action = () => downloader.DownloadAsync(asset, null, CancellationToken.None);

        await action.Should().ThrowAsync<IOException>();
        Directory.EnumerateFiles(_directory, "*.part").Should().BeEmpty();
    }

    /// <summary>HTTP 失败转成 IO 异常。</summary>
    [Fact]
    public async Task HTTP失败转为IO异常()
    {
        var downloader = Create([], HttpStatusCode.InternalServerError);
        var asset = new UpdateAsset("github", "https://example.invalid/setup.exe", string.Empty, 0);

        var action = () => downloader.DownloadAsync(asset, null, CancellationToken.None);

        await action.Should().ThrowAsync<IOException>();
    }

    private UpdateDownloader Create(byte[] payload, HttpStatusCode status = HttpStatusCode.OK)
        => new(new HttpClient(new StubHandler(status, payload)), _directory);

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private sealed class StubHandler(HttpStatusCode status, byte[] payload) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(status)
            {
                Content = new ByteArrayContent(payload),
            };

            response.Content.Headers.ContentLength = payload.Length;
            return Task.FromResult(response);
        }
    }
}

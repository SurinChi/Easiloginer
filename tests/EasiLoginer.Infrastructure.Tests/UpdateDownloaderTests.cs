using System.Net;
using System.Security.Cryptography;
using System.Text;
using EasiLoginer.Application.Update;
using EasiLoginer.Domain.Configuration;
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

    /// <summary>自动选择：直连 GitHub 失败后应回退到 ghfast 镜像。</summary>
    [Fact]
    public async Task 自动选择回退到镜像()
    {
        var bytes = Encoding.UTF8.GetBytes("mirror-payload");
        var handler = new RecordingHandler(request =>
        {
            if (request.RequestUri!.Host == "github.com")
            {
                throw new HttpRequestException("direct blocked");
            }

            return CreateResponse(HttpStatusCode.OK, bytes);
        });

        using var http = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        var downloader = new UpdateDownloader(http, _directory, () => DownloadSource.Auto);
        var asset = new UpdateAsset(
            "github",
            "https://github.com/SurinChi/Easiloginer/releases/download/v0.2.0-alpha.1/EasiLoginer-Setup-0.2.0-alpha.1.exe",
            string.Empty,
            bytes.Length);

        var result = await downloader.DownloadAsync(asset, null, CancellationToken.None);

        (await File.ReadAllBytesAsync(result.FilePath)).Should().Equal(bytes);
        handler.Requests.Should().HaveCount(2);
        handler.Requests[0].Should().StartWith("https://github.com/");
        handler.Requests[1].Should().StartWith("https://ghfast.top/https://github.com/");
    }

    /// <summary>指定 ghfast 时只请求镜像，不先请求直连。</summary>
    [Fact]
    public async Task 指定镜像只使用镜像()
    {
        var bytes = Encoding.UTF8.GetBytes("ghfast-payload");
        var handler = new RecordingHandler(_ => CreateResponse(HttpStatusCode.OK, bytes));

        using var http = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        var downloader = new UpdateDownloader(http, _directory, () => DownloadSource.GhFast);
        var asset = new UpdateAsset(
            "github",
            "https://github.com/SurinChi/Easiloginer/releases/download/v0.2.0-alpha.1/EasiLoginer-Setup.exe",
            string.Empty,
            bytes.Length);

        await downloader.DownloadAsync(asset, null, CancellationToken.None);

        handler.Requests.Should().ContainSingle();
        handler.Requests[0].Should().StartWith("https://ghfast.top/https://github.com/");
    }

    /// <summary>非 GitHub 资产（例如以后接 Gitee）不能被错误地加上 GitHub 镜像前缀。</summary>
    [Fact]
    public async Task 非GitHub资产不镜像()
    {
        var bytes = Encoding.UTF8.GetBytes("gitee-payload");
        var handler = new RecordingHandler(_ => CreateResponse(HttpStatusCode.OK, bytes));

        using var http = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        var downloader = new UpdateDownloader(http, _directory, () => DownloadSource.Auto);
        const string url = "https://gitee.com/SurinChi/Easiloginer/releases/download/v0.2.0-alpha.1/setup.exe";
        var asset = new UpdateAsset("gitee", url, string.Empty, bytes.Length);

        await downloader.DownloadAsync(asset, null, CancellationToken.None);

        handler.Requests.Should().ContainSingle().Which.Should().Be(url);
    }

    /// <summary>连接建立但长时间收不到数据时，给出明确的“下载停滞”而不是笼统的取消。</summary>
    [Fact]
    public async Task 无数据停滞有明确提示()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(new SlowStream()),
        });

        using var http = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        var downloader = new UpdateDownloader(
            http,
            _directory,
            () => DownloadSource.GitHub,
            headerTimeout: TimeSpan.FromSeconds(1),
            inactivityTimeout: TimeSpan.FromMilliseconds(50));
        var asset = new UpdateAsset(
            "github",
            "https://github.com/SurinChi/Easiloginer/releases/download/v0.2.0-alpha.1/EasiLoginer-Setup.exe",
            string.Empty,
            0);

        var action = () => downloader.DownloadAsync(asset, null, CancellationToken.None);

        var exception = await action.Should().ThrowAsync<IOException>();
        exception.Which.Message.Should().Contain("停滞");
        Directory.EnumerateFiles(_directory, "*.part").Should().BeEmpty();
    }

    private UpdateDownloader Create(byte[] payload, HttpStatusCode status = HttpStatusCode.OK)
        => new(new HttpClient(new StubHandler(status, payload)), _directory);

    private static HttpResponseMessage CreateResponse(HttpStatusCode status, byte[] payload)
    {
        var response = new HttpResponseMessage(status)
        {
            Content = new ByteArrayContent(payload),
        };

        response.Content.Headers.ContentLength = payload.Length;
        return response;
    }

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
            => Task.FromResult(CreateResponse(status, payload));
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!.ToString());
            return Task.FromResult(respond(request));
        }
    }

    /// <summary>建立连接后永远不返回数据的流，用来验证停滞超时。</summary>
    private sealed class SlowStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => 0;

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            return 0;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}

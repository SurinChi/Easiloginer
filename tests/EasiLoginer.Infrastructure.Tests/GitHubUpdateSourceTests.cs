using System.Net;
using System.Text;
using System.Text.Json;
using EasiLoginer.Application.Update;
using EasiLoginer.Domain.Configuration;
using EasiLoginer.Infrastructure.Update;
using FluentAssertions;

namespace EasiLoginer.Infrastructure.Tests;

/// <summary>GitHub Releases → 自有清单的转换（文档 §12.4）。</summary>
public sealed class GitHubUpdateSourceTests
{
    /// <summary>稳定版：tag 去 v 前缀、资产与 digest 映射。</summary>
    [Fact]
    public async Task 稳定版清单映射()
    {
        const string payload = """
            {
              "tag_name": "v0.0.2",
              "prerelease": false,
              "body": "修复若干问题",
              "assets": [
                {
                  "name": "EasiLoginer_0.0.2.zip",
                  "size": 1024,
                  "browser_download_url": "https://example.invalid/EasiLoginer_0.0.2.zip",
                  "digest": "sha256:deadbeef"
                }
              ]
            }
            """;

        var source = CreateSource(_ => Json(HttpStatusCode.OK, payload));

        var json = await source.FetchManifestJsonAsync(UpdateChannel.Release, CancellationToken.None);

        json.Should().NotBeNull();
        using var document = JsonDocument.Parse(json!);
        var root = document.RootElement;

        root.GetProperty("version").GetString().Should().Be("0.0.2");
        root.GetProperty("channel").GetString().Should().Be("release");
        root.GetProperty("notes").GetString().Should().Be("修复若干问题");

        var asset = root.GetProperty("assets")[0];
        asset.GetProperty("url").GetString().Should().Be("https://example.invalid/EasiLoginer_0.0.2.zip");
        asset.GetProperty("sha256").GetString().Should().Be("deadbeef");
        asset.GetProperty("size").GetInt64().Should().Be(1024);
        asset.GetProperty("source").GetString().Should().Be("github");
    }

    /// <summary>Dev 通道取列表第一条。</summary>
    [Fact]
    public async Task 开发通道取列表首条()
    {
        const string payload = """
            [
              { "tag_name": "0.0.3b1", "body": "beta", "assets": [] },
              { "tag_name": "0.0.2", "body": "stable", "assets": [] }
            ]
            """;

        var source = CreateSource(_ => Json(HttpStatusCode.OK, payload));

        var json = await source.FetchManifestJsonAsync(UpdateChannel.Dev, CancellationToken.None);

        json.Should().NotBeNull();
        using var document = JsonDocument.Parse(json!);
        document.RootElement.GetProperty("version").GetString().Should().Be("0.0.3b1");
        document.RootElement.GetProperty("channel").GetString().Should().Be("dev");
    }

    /// <summary>没有任何发布（404）视为"无更新源"。</summary>
    [Fact]
    public async Task 没有发布返回空()
    {
        var source = CreateSource(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        var json = await source.FetchManifestJsonAsync(UpdateChannel.Release, CancellationToken.None);

        json.Should().BeNull();
    }

    /// <summary>Dev 通道空列表同样返回空。</summary>
    [Fact]
    public async Task 开发通道空列表()
    {
        var source = CreateSource(_ => Json(HttpStatusCode.OK, "[]"));

        var json = await source.FetchManifestJsonAsync(UpdateChannel.Dev, CancellationToken.None);

        json.Should().BeNull();
    }

    /// <summary>网络异常按端口约定转成 <see cref="IOException"/>。</summary>
    [Fact]
    public async Task 网络异常转为IO异常()
    {
        var source = CreateSource(_ => throw new HttpRequestException("boom"));

        var action = () => source.FetchManifestJsonAsync(UpdateChannel.Release, CancellationToken.None);

        await action.Should().ThrowAsync<IOException>();
    }

    /// <summary>请求地址与 User-Agent 符合 GitHub API 要求。</summary>
    [Fact]
    public async Task 请求头与地址()
    {
        HttpRequestMessage? captured = null;
        var source = CreateSource(request =>
        {
            captured = request;
            return Json(HttpStatusCode.OK, """{ "tag_name": "0.0.2", "assets": [] }""");
        });

        await source.FetchManifestJsonAsync(UpdateChannel.Release, CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.RequestUri!.ToString().Should().Be("https://api.github.com/repos/SurinChi/EasiLoginer/releases/latest");
        captured.Headers.UserAgent.ToString().Should().Contain("EasiLoginer");
        captured.Headers.Accept.ToString().Should().Contain("application/vnd.github+json");
    }

    private static GitHubUpdateSource CreateSource(Func<HttpRequestMessage, HttpResponseMessage> respond)
        => new(new HttpClient(new StubHandler(respond)));

    private static HttpResponseMessage Json(HttpStatusCode status, string payload)
        => new(status) { Content = new StringContent(payload, Encoding.UTF8, "application/json") };

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(respond(request));
    }
}

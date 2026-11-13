using System.Net;
using System.Text;
using EasiLoginer.Infrastructure.Announcements;
using FluentAssertions;

namespace EasiLoginer.Infrastructure.Tests;

/// <summary>公告清单来源：远程地址、本地优先与失败语义（文档 §6.6）。</summary>
public sealed class HttpAnnouncementSourceTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "EasiLoginerTests", "announcements", Guid.NewGuid().ToString("N"));

    public HttpAnnouncementSourceTests() => Directory.CreateDirectory(_directory);

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    /// <summary>存在本地清单时直接读取，不发网络请求。</summary>
    [Fact]
    public async Task 本地文件优先()
    {
        var path = Path.Combine(_directory, "announcements.json");
        await File.WriteAllTextAsync(path, """[{ "id": "local", "title": "本地" }]""");

        var source = new HttpAnnouncementSource(
            new HttpClient(new StubHandler(_ => throw new InvalidOperationException("不应发起网络请求"))),
            localFilePath: path);

        var json = await source.FetchAsync(CancellationToken.None);

        json.Should().Contain("\"local\"");
    }

    /// <summary>默认地址与 User-Agent 符合约定。</summary>
    [Fact]
    public async Task 请求地址与请求头()
    {
        HttpRequestMessage? captured = null;
        var source = new HttpAnnouncementSource(new HttpClient(new StubHandler(request =>
        {
            captured = request;
            return Json(HttpStatusCode.OK, """{ "announcements": [] }""");
        })));

        await source.FetchAsync(CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.RequestUri!.ToString().Should().Be(HttpAnnouncementSource.DefaultEndpoint);
        captured.Headers.UserAgent.ToString().Should().Contain("EasiLoginer");
    }

    /// <summary>没有发布公告时返回 null（不是错误）。</summary>
    [Fact]
    public async Task 四零四返回空()
    {
        var source = new HttpAnnouncementSource(
            new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound))));

        var json = await source.FetchAsync(CancellationToken.None);

        json.Should().BeNull();
    }

    /// <summary>网络异常统一转成 IO 异常，交给应用层静默降级。</summary>
    [Fact]
    public async Task 网络异常转为IO异常()
    {
        var source = new HttpAnnouncementSource(
            new HttpClient(new StubHandler(_ => throw new HttpRequestException("boom"))));

        var action = () => source.FetchAsync(CancellationToken.None);

        await action.Should().ThrowAsync<IOException>();
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string payload)
        => new(status) { Content = new StringContent(payload, Encoding.UTF8, "application/json") };

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(respond(request));
    }
}

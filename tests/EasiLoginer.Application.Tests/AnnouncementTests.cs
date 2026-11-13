using Microsoft.Extensions.Logging.Abstractions;
using EasiLoginer.Application.Announcements;
using EasiLoginer.Domain.Announcements;
using EasiLoginer.Domain.Versioning;
using FluentAssertions;

namespace EasiLoginer.Application.Tests;

/// <summary>公告清单解析与公告用例（文档 §6.6）。</summary>
public sealed class AnnouncementTests
{
    /// <summary>对象根 + 完整字段解析。</summary>
    [Fact]
    public void 解析对象根()
    {
        const string json = """
            {
              "announcements": [
                {
                  "id": "v0.1.3",
                  "title": "0.1.3 已发布",
                  "body": "修复若干问题\n欢迎反馈",
                  "severity": "warning",
                  "published_at": "2026-11-13T10:00:00+08:00",
                  "start_at": "2026-11-13T00:00:00+08:00",
                  "end_at": "2026-12-01T00:00:00+08:00",
                  "min_version": "0.1.0",
                  "max_version": "0.2.0",
                  "url": "https://github.com/SurinChi/Easiloginer/releases",
                  "dismissible": false
                }
              ]
            }
            """;

        var items = AnnouncementManifestParser.Parse(json);

        var item = items.Should().ContainSingle().Subject;
        item.Id.Should().Be("v0.1.3");
        item.Title.Should().Be("0.1.3 已发布");
        item.Body.Should().Contain("欢迎反馈");
        item.Severity.Should().Be(AnnouncementSeverity.Warning);
        item.StartAt.Should().Be(new DateTimeOffset(2026, 11, 13, 0, 0, 0, TimeSpan.FromHours(8)));
        item.EndAt.Should().Be(new DateTimeOffset(2026, 12, 1, 0, 0, 0, TimeSpan.FromHours(8)));
        item.MinVersion.Should().Be("0.1.0");
        item.MaxVersion.Should().Be("0.2.0");
        item.Url.Should().Be("https://github.com/SurinChi/Easiloginer/releases");
        item.Dismissible.Should().BeFalse();
    }

    /// <summary>根节点也可以直接是数组。</summary>
    [Fact]
    public void 解析数组根()
    {
        var items = AnnouncementManifestParser.Parse("""[{ "id": "a", "title": "标题", "published_at": "2026-11-13" }]""");

        items.Should().ContainSingle().Which.Id.Should().Be("a");
    }

    /// <summary>缺少 id / title 的条目跳过，不会影响其它条目。</summary>
    [Fact]
    public void 跳过无效条目()
    {
        const string json = """
            [
              { "title": "没有 id", "published_at": "2026-11-13" },
              { "id": "no-title", "published_at": "2026-11-13" },
              { "id": "bad-url", "title": "标题", "published_at": "2026-11-13", "url": "javascript:alert(1)" },
              { "id": "ok", "title": "正常", "published_at": "2026-11-13" }
            ]
            """;

        var items = AnnouncementManifestParser.Parse(json);

        items.Select(item => item.Id).Should().Equal("bad-url", "ok");
        items[0].Url.Should().BeNull("只接受 http/https 链接");
    }

    /// <summary>重复 ID 只保留第一条。</summary>
    [Fact]
    public void 重复ID去重()
    {
        const string json = """
            [
              { "id": "same", "title": "第一条", "published_at": "2026-11-13" },
              { "id": "same", "title": "第二条", "published_at": "2026-11-14" }
            ]
            """;

        var items = AnnouncementManifestParser.Parse(json);

        items.Should().ContainSingle().Which.Title.Should().Be("第一条");
    }

    /// <summary>缺少 announcements 数组时明确报错。</summary>
    [Fact]
    public void 缺少公告数组报错()
    {
        var action = () => AnnouncementManifestParser.Parse("""{ "schema": 1 }""");

        action.Should().Throw<FormatException>();
    }

    /// <summary>公告服务按当前版本与隐藏记录过滤。</summary>
    [Fact]
    public async Task 服务按规则过滤()
    {
        const string json = """
            [
              { "id": "old", "title": "旧公告", "published_at": "2026-11-01", "max_version": "0.1.3" },
              { "id": "hidden", "title": "被隐藏", "published_at": "2026-11-02" },
              { "id": "shown", "title": "当前公告", "published_at": "2026-11-03" }
            ]
            """;

        var service = new AnnouncementService(new StubSource(json), NullLogger<AnnouncementService>.Instance);

        var items = await service.FetchAsync(
            Pep440Version.Parse("0.1.3"), ["hidden"], CancellationToken.None);

        items.Should().ContainSingle().Which.Id.Should().Be("shown");
    }

    /// <summary>来源失败时返回空列表，绝不抛给界面。</summary>
    [Fact]
    public async Task 来源失败时静默降级()
    {
        var service = new AnnouncementService(new StubSource(new IOException("boom")), NullLogger<AnnouncementService>.Instance);

        var items = await service.FetchAsync(Pep440Version.Parse("0.1.3"), null, CancellationToken.None);

        items.Should().BeEmpty();
    }

    /// <summary>清单写坏时同样只是不显示。</summary>
    [Fact]
    public async Task 清单损坏时静默降级()
    {
        var service = new AnnouncementService(new StubSource("{ not json"), NullLogger<AnnouncementService>.Instance);

        var items = await service.FetchAsync(Pep440Version.Parse("0.1.3"), null, CancellationToken.None);

        items.Should().BeEmpty();
    }

    /// <summary>没有公告源（null）时视为没有公告。</summary>
    [Fact]
    public async Task 没有来源时返回空()
    {
        var service = new AnnouncementService(new StubSource((string?)null), NullLogger<AnnouncementService>.Instance);

        var items = await service.FetchAsync(Pep440Version.Parse("0.1.3"), null, CancellationToken.None);

        items.Should().BeEmpty();
    }

    private sealed class StubSource : IAnnouncementSource
    {
        private readonly string? _json;
        private readonly Exception? _error;

        public StubSource(string? json) => _json = json;

        public StubSource(Exception error) => _error = error;

        public Task<string?> FetchAsync(CancellationToken cancellationToken)
            => _error is null ? Task.FromResult(_json) : Task.FromException<string?>(_error);
    }
}

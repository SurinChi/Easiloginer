using EasiLoginer.Domain.Announcements;
using EasiLoginer.Domain.Versioning;
using FluentAssertions;

namespace EasiLoginer.Domain.Tests;

/// <summary>
/// 公告展示规则（文档 §6.6）：时间窗口、版本范围、隐藏记录、排序与条数上限。
/// </summary>
public sealed class AnnouncementPolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 13, 12, 0, 0, TimeSpan.FromHours(8));

    /// <summary>只展示处于时间窗口内的公告。</summary>
    [Fact]
    public void 时间窗口过滤()
    {
        var items = new[]
        {
            Create("before", publishedAt: Now.AddDays(-3), start: Now.AddDays(1)),
            Create("active", publishedAt: Now.AddHours(-2), start: Now.AddDays(-1), end: Now.AddDays(1)),
            Create("expired", publishedAt: Now.AddDays(-4), start: Now.AddDays(-5), end: Now.AddDays(-1)),
            Create("forever", publishedAt: Now.AddHours(-1)),
        };

        var visible = AnnouncementPolicy.Select(items, Pep440Version.Parse("0.1.3"), Now);

        visible.Select(item => item.Id).Should().Equal("forever", "active");
    }

    /// <summary>版本范围：min 含、max 不含；不可解析的边界按「不限制」处理。</summary>
    [Fact]
    public void 版本范围过滤()
    {
        var items = new[]
        {
            Create("new-only", minVersion: "0.2.0"),
            Create("at-max", maxVersion: "0.1.3"),
            Create("below-max", maxVersion: "0.1.4"),
            Create("all", minVersion: "0.1.0", maxVersion: "0.2.0"),
            Create("typo", minVersion: "not-a-version"),
        };

        var visible = AnnouncementPolicy.Select(items, Pep440Version.Parse("0.1.3"), Now);

        visible.Select(item => item.Id).Should().BeEquivalentTo(["below-max", "all", "typo"]);
        visible.Select(item => item.Id).Should().NotContain("at-max", "max_version 是开区间");
    }

    /// <summary>已隐藏的 ID 不再出现。</summary>
    [Fact]
    public void 隐藏记录生效()
    {
        var items = new[] { Create("a"), Create("b") };

        var visible = AnnouncementPolicy.Select(items, Pep440Version.Parse("0.1.3"), Now, ["a"]);

        visible.Should().ContainSingle().Which.Id.Should().Be("b");
    }

    /// <summary>按发布时间倒序，并且最多 3 条。</summary>
    [Fact]
    public void 排序与条数上限()
    {
        var items = Enumerable.Range(0, 5)
            .Select(index => Create($"id-{index}", publishedAt: Now.AddHours(-index)))
            .ToArray();

        var visible = AnnouncementPolicy.Select(items, Pep440Version.Parse("0.1.3"), Now);

        visible.Should().HaveCount(AnnouncementPolicy.MaxVisible);
        visible.Select(item => item.Id).Should().Equal("id-0", "id-1", "id-2");
    }

    /// <summary>同一发布时间按 ID 稳定排序，避免每次启动顺序跳动。</summary>
    [Fact]
    public void 同一时间按ID稳定排序()
    {
        var items = new[] { Create("b"), Create("a") };

        AnnouncementPolicy.Select(items, Pep440Version.Parse("0.1.3"), Now)
            .Select(item => item.Id)
            .Should().Equal("a", "b");
    }

    private static Announcement Create(
        string id,
        DateTimeOffset? publishedAt = null,
        DateTimeOffset? start = null,
        DateTimeOffset? end = null,
        string? minVersion = null,
        string? maxVersion = null)
        => new(
            id,
            $"标题 {id}",
            $"正文 {id}",
            AnnouncementSeverity.Info,
            publishedAt ?? Now.AddHours(-1),
            start,
            end,
            minVersion,
            maxVersion);
}

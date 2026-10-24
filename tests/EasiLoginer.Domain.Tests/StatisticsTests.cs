using EasiLoginer.Domain.Automation;
using EasiLoginer.Domain.Configuration;
using FluentAssertions;

namespace EasiLoginer.Domain.Tests;

/// <summary>统计计数（文档 §2.1 第 5 条 / §5.1）。</summary>
public sealed class StatisticsTests
{
    /// <summary>记录开始：总数与按账号计数同时增加，键是脱敏哈希。</summary>
    [Fact]
    public void 记录开始同时累加总数与按账号计数()
    {
        var statistics = new StatisticsConfig();

        statistics.RecordStart("student@example.com");
        statistics.RecordStart("student@example.com");
        statistics.RecordStart("other@example.com");

        statistics.LoginCounts.Should().Be(3);
        statistics.LoginCountsPerAccount.Should().HaveCount(2);
        statistics.LoginCountsPerAccount["616bb35d31d0a684"].Should().Be(2, "同一账号必须聚合到同一脱敏键");
    }

    /// <summary>失败次数由总数推出，不单独计数。</summary>
    [Fact]
    public void 失败次数由总数推出()
    {
        var statistics = new StatisticsConfig();

        statistics.RecordStart("a");
        statistics.RecordStart("b");
        statistics.RecordResult(LoginOutcome.Success, 10);
        statistics.RecordResult(LoginOutcome.Cancelled, 5);

        statistics.LoginCounts.Should().Be(2);
        statistics.LoginSuccessCounts.Should().Be(1);
        statistics.LoginInterruptCounts.Should().Be(1);
        statistics.LoginFailureCounts.Should().Be(0);
    }

    /// <summary>耗时累计并保留最大值（含未成功的流程）。</summary>
    [Fact]
    public void 耗时累计与最大值()
    {
        var statistics = new StatisticsConfig();

        statistics.RecordResult(LoginOutcome.Success, 12.5);
        statistics.RecordResult(LoginOutcome.Failed, 30.25);
        statistics.RecordResult(LoginOutcome.Cancelled, 3);

        statistics.TotalLoginTime.Should().BeApproximately(45.75, 1e-9);
        statistics.MaxLoginTime.Should().BeApproximately(30.25, 1e-9);
    }

    /// <summary>遗留明文账号键在归一化时被替换为哈希，且计数合并。</summary>
    [Fact]
    public void 明文账号键被迁移为哈希()
    {
        var statistics = new StatisticsConfig
        {
            LoginCountsPerAccount = new Dictionary<string, int>
            {
                ["student@example.com"] = 3,
                ["616bb35d31d0a684"] = 2,
                ["0123456789abcdef"] = 1,
            },
        };

        statistics.NormalizeAccountKeys();

        statistics.LoginCountsPerAccount.Should().NotContainKey("student@example.com");
        statistics.LoginCountsPerAccount["616bb35d31d0a684"].Should().Be(5, "同一账号的旧明文计数应合并到哈希键");
        statistics.LoginCountsPerAccount["0123456789abcdef"].Should().Be(1);
    }

    /// <summary>已经是哈希的键保持不变（幂等）。</summary>
    [Fact]
    public void 归一化是幂等的()
    {
        var statistics = new StatisticsConfig
        {
            LoginCountsPerAccount = new Dictionary<string, int> { ["616bb35d31d0a684"] = 4 },
        };

        statistics.NormalizeAccountKeys();

        statistics.LoginCountsPerAccount.Should().ContainSingle()
            .Which.Should().Be(new KeyValuePair<string, int>("616bb35d31d0a684", 4));
    }
}

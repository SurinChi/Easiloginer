using EasiLoginer.Domain.Retention;
using FluentAssertions;

namespace EasiLoginer.Domain.Tests;

/// <summary>长期未启动的数据清理策略（最短 7 天，0 关闭）。</summary>
public sealed class DataRetentionPolicyTests
{
    /// <summary>0 与负数都表示关闭。</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void 关闭(int days)
    {
        DataRetentionPolicy.Normalize(days).Should().Be(0);
        DataRetentionPolicy
            .ShouldReset(DateTimeOffset.UtcNow.AddYears(-5), DateTimeOffset.UtcNow, days)
            .Should().BeFalse();
    }

    /// <summary>启用时最短 7 天，小值被抬到 7。</summary>
    [Theory]
    [InlineData(1, 7)]
    [InlineData(6, 7)]
    [InlineData(7, 7)]
    [InlineData(30, 30)]
    [InlineData(90, 90)]
    public void 归一化(int configured, int expected)
    {
        DataRetentionPolicy.Normalize(configured).Should().Be(expected);
    }

    /// <summary>正好到阈值即清理。</summary>
    [Fact]
    public void 到期即清理()
    {
        var now = new DateTimeOffset(2026, 10, 30, 12, 0, 0, TimeSpan.Zero);

        DataRetentionPolicy.ShouldReset(now.AddDays(-90), now, 90).Should().BeTrue();
        DataRetentionPolicy.ShouldReset(now.AddDays(-89), now, 90).Should().BeFalse();
    }

    /// <summary>小于 7 天的配置按 7 天生效（防止"启动即清空"）。</summary>
    [Fact]
    public void 配置过小按七天()
    {
        var now = new DateTimeOffset(2026, 10, 30, 12, 0, 0, TimeSpan.Zero);

        DataRetentionPolicy.ShouldReset(now.AddDays(-6), now, 1).Should().BeFalse();
        DataRetentionPolicy.ShouldReset(now.AddDays(-7), now, 1).Should().BeTrue();
    }

    /// <summary>没有历史记录（首次启动）不清理。</summary>
    [Fact]
    public void 首次启动不清理()
    {
        DataRetentionPolicy
            .ShouldReset(null, DateTimeOffset.UtcNow, 90)
            .Should().BeFalse();
    }
}

using EasiLoginer.Application.Observability;
using FluentAssertions;

namespace EasiLoginer.Application.Tests;

/// <summary>遥测脱敏：密文与手机号必须打码（文档 §5.1 隐私要求）。</summary>
public sealed class SensitiveDataScrubberTests
{
    /// <summary>Fernet 密文整体替换。</summary>
    [Fact]
    public void 密文被打码()
    {
        const string text = "account=ea$gAAAAABk9-abcDEF_123==&password=ea$gAAAAABk9-zzz";

        var scrubbed = SensitiveDataScrubber.Scrub(text);

        scrubbed.Should().Be($"account={SensitiveDataScrubber.SecretMask}&password={SensitiveDataScrubber.SecretMask}");
    }

    /// <summary>11 位手机号被打码。</summary>
    [Fact]
    public void 手机号被打码()
    {
        var scrubbed = SensitiveDataScrubber.Scrub("登录失败，账号 13800000000 被风控");

        scrubbed.Should().Be($"登录失败，账号 {SensitiveDataScrubber.PhoneMask} 被风控");
    }

    /// <summary>密文与手机号同时出现时都处理。</summary>
    [Fact]
    public void 同时处理()
    {
        var scrubbed = SensitiveDataScrubber.Scrub("ea$abc123 for 15777420786");

        scrubbed.Should().Be($"{SensitiveDataScrubber.SecretMask} for {SensitiveDataScrubber.PhoneMask}");
    }

    /// <summary>普通文本保持不变（不能误伤版本号、时间戳等）。</summary>
    [Theory]
    [InlineData("登录成功，耗时 10.41 秒")]
    [InlineData("version 0.0.1 build 20261024")]
    [InlineData("order id 1234567890123")]
    public void 普通文本不变(string text)
    {
        SensitiveDataScrubber.Scrub(text).Should().Be(text);
    }

    /// <summary>空值原样返回。</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void 空值原样返回(string? text)
    {
        SensitiveDataScrubber.Scrub(text).Should().Be(text);
    }
}

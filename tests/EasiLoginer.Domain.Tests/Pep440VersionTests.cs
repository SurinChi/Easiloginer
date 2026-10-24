using EasiLoginer.Domain.Versioning;
using FluentAssertions;

namespace EasiLoginer.Domain.Tests;

/// <summary>
/// PEP 440 解析与排序，用例覆盖文档 §5.4 与 §13.2 的要求。
/// </summary>
public sealed class Pep440VersionTests
{
    /// <summary>文档点名的四组比较必须成立。</summary>
    [Theory]
    [InlineData("1.2.2b6", "1.2.2")]
    [InlineData("1.2.0rc1", "1.2.0")]
    [InlineData("1.1.4", "1.1.4r1")]
    [InlineData("1.2.0.dev1", "1.2.0a1")]
    [InlineData("1.2.0a1", "1.2.0b1")]
    [InlineData("1.2.0b1", "1.2.0rc1")]
    [InlineData("1.0.0", "1.0.0.post1")]
    [InlineData("1.99.99", "2.0.0")]
    public void 小于关系成立(string smaller, string larger)
    {
        var left = Pep440Version.Parse(smaller);
        var right = Pep440Version.Parse(larger);

        left.CompareTo(right).Should().BeLessThan(0);
        right.CompareTo(left).Should().BeGreaterThan(0);
        left.Equals(right).Should().BeFalse();
    }

    /// <summary>PEP 440 归一化：补零与 v 前缀不改变版本。</summary>
    [Theory]
    [InlineData("1.2", "1.2.0")]
    [InlineData("v1.2", "1.2.0")]
    [InlineData("1.2.0alpha1", "1.2.0a1")]
    [InlineData("1.2.0pre1", "1.2.0rc1")]
    [InlineData("1.2.0rev2", "1.2.0.post2")]
    public void 归一化后相等(string left, string right)
    {
        var a = Pep440Version.Parse(left);
        var b = Pep440Version.Parse(right);

        a.CompareTo(b).Should().Be(0);
        a.Should().Be(b);
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    /// <summary>epoch 优先于一切。</summary>
    [Fact]
    public void 纪元优先()
    {
        Pep440Version.Parse("1!0.1").Should().BeGreaterThan(Pep440Version.Parse("99.99"));
    }

    /// <summary>dev 段排在同版本 pre 之前，但排在更早的 release 之后。</summary>
    [Fact]
    public void dev段的位置()
    {
        Pep440Version.Parse("1.2.0.dev1").Should().BeLessThan(Pep440Version.Parse("1.2.0a1"));
        Pep440Version.Parse("1.1.99").Should().BeLessThan(Pep440Version.Parse("1.2.0.dev1"));
    }

    /// <summary>归一化输出：更新日志按此展示。</summary>
    [Theory]
    [InlineData("1.2.2b6", "1.2.2b6")]
    [InlineData("1.2.0.dev1", "1.2.0.dev1")]
    [InlineData("1.2.0rc1", "1.2.0rc1")]
    [InlineData("1.1.4r1", "1.1.4.post1")]
    [InlineData("v2.0", "2.0")]
    public void 输出归一化文本(string input, string expected)
    {
        Pep440Version.Parse(input).ToString().Should().Be(expected);
    }

    /// <summary>非法输入必须失败，不能"尽量解析"。</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("1.2..3")]
    [InlineData("1.2.0x1")]
    [InlineData("1.2.0-1")]
    [InlineData("1.")]
    public void 非法输入解析失败(string input)
    {
        Pep440Version.TryParse(input, out var version).Should().BeFalse();
        version.Should().BeNull();
    }

    /// <summary>排序结果必须与 PEP 440 一致。</summary>
    [Fact]
    public void 排序稳定()
    {
        var inputs = new[] { "1.2.0", "1.2.0a1", "1.1.9", "1.2.0rc1", "1.2.0.dev1", "1.2.0.post1" };
        var sorted = inputs.Select(Pep440Version.Parse).Order().Select(v => v.Text).ToArray();

        sorted.Should().ContainInOrder("1.1.9", "1.2.0.dev1", "1.2.0a1", "1.2.0rc1", "1.2.0", "1.2.0.post1");
    }
}

using System.Text;
using EasiLoginer.Integrations.EasiNote.Environment;
using FluentAssertions;

namespace EasiLoginer.Integrations.Tests;

/// <summary><c>Configs.fkv</c> 解析（文档 §8.2）。</summary>
public sealed class ConfigsFkvTests
{
    private const string Sample = """
        > 配置文件
        DeviceInfo.Type
        {228756AD-0620-497E-94FB-4022DB99C853}
        >
        DeviceCache.IsIwb
        true
        >
        """;

    /// <summary>读取单行值。</summary>
    [Fact]
    public void 读取单行值()
    {
        ConfigsFkv.ReadValue(Sample, "DeviceCache.IsIwb").Should().Be("true");
    }

    /// <summary>以 <c>&gt;</c> 开头的其他行是注释，不参与记录。</summary>
    [Fact]
    public void 注释行被跳过()
    {
        ConfigsFkv.ReadValue(Sample, "DeviceInfo.Type")
            .Should().Be("{228756AD-0620-497E-94FB-4022DB99C853}");
    }

    /// <summary>多行值用 <c>\n</c> 连接返回。</summary>
    [Fact]
    public void 多行值被连接()
    {
        const string text = "Key\nline1\nline2\n>\n";

        ConfigsFkv.ReadValue(text, "Key").Should().Be("line1\nline2");
    }

    /// <summary>不存在的键返回 null。</summary>
    [Fact]
    public void 不存在的键返回空()
    {
        ConfigsFkv.ReadValue(Sample, "No.Such.Key").Should().BeNull();
    }

    /// <summary>
    /// 必须照抄的怪癖：只有遇到下一个 <c>&gt;</c> 才返回上一条记录，
    /// 因此文件末尾没有分隔行的记录读不到。
    /// </summary>
    [Fact]
    public void 末尾无分隔行的记录读不到()
    {
        ConfigsFkv.ReadValue("Key\nvalue", "Key").Should().BeNull();
    }

    /// <summary>只有键行、没有值行的记录不算有效记录。</summary>
    [Fact]
    public void 只有键行的记录被忽略()
    {
        ConfigsFkv.ReadValue("Key\n>\nOther\nv\n>\n", "Key").Should().BeNull();
    }

    /// <summary>键必须完全匹配（区分大小写）。</summary>
    [Fact]
    public void 键名区分大小写()
    {
        ConfigsFkv.ReadValue("deviceinfo.type\nX\n>\n", "DeviceInfo.Type").Should().BeNull();
    }

    /// <summary>文件带 BOM 时必须能正确解析。</summary>
    [Fact]
    public void 文件BOM被正确处理()
    {
        var path = Path.Combine(Path.GetTempPath(), "EasiLoginerTests", "fkv", Guid.NewGuid().ToString("N") + ".fkv");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, Sample, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

            ConfigsFkv.ReadValueFromFile(path, "DeviceCache.IsIwb").Should().Be("true");
        }
        finally
        {
            var directory = Path.GetDirectoryName(path)!;
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    /// <summary>文件不存在时返回 null，而不是抛异常。</summary>
    [Fact]
    public void 文件不存在时返回空()
    {
        ConfigsFkv.ReadValueFromFile(Path.Combine(Path.GetTempPath(), "no-such-file.fkv"), "X").Should().BeNull();
    }
}

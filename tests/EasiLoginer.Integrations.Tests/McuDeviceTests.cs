using EasiLoginer.Integrations.EasiNote.Environment;
using FluentAssertions;

namespace EasiLoginer.Integrations.Tests;

/// <summary>HID 设备判定（文档 §8.2 的定点表与兜底规则）。</summary>
public sealed class McuDeviceTests
{
    private const string Template = @"\\?\hid#vid_{0}&pid_{1}&{2}#8&1&0&0000#{{4d1e55b2-f16f-11cf-88cb-001111000030}}";

    [Fact]
    public void 解析设备路径()
    {
        var device = new McuDevice(string.Format(Template, "0f15", "1ff7", "mi_00"));

        device.Vid.Should().Be(0x0F15);
        device.Pid.Should().Be(0x1FF7);
        device.Key.Should().Be("mi_00");
    }

    /// <summary>
    /// 定点表元组语义是 <c>(pid, vid, key)</c>：条目 (0x0F15, 0x1FF7, "mi_00")
    /// 对应设备路径 <c>vid_1ff7&amp;pid_0f15&amp;mi_00</c>。
    /// </summary>
    [Fact]
    public void 定点表命中()
    {
        var device = new McuDevice(string.Format(Template, "1ff7", "0f15", "mi_00"));

        device.IsSeewoMcu().Should().BeTrue();
    }

    /// <summary>首条 (0x1FF7, 0x0F15, "mi_00&amp;col02") 写法看起来像 (vid, pid)，但同样按 (pid, vid) 比较。</summary>
    [Fact]
    public void 定点表首条命中()
    {
        var device = new McuDevice(string.Format(Template, "0f15", "1ff7", "mi_00&col02"));

        device.IsSeewoMcu().Should().BeTrue();
    }

    /// <summary>定点表的空 key 条目只比较 PID/VID。</summary>
    [Fact]
    public void 空key条目只比较PID与VID()
    {
        var device = new McuDevice(@"\\?\hid#vid_1ff7&pid_0f33#8&1&0&0000");

        device.IsSeewoMcu().Should().BeTrue();
    }

    /// <summary>兜底规则：VID 命中且 PID 落在区间内、接口键含提示词。</summary>
    [Theory]
    [InlineData("222a", "0010", "mi_00")]
    [InlineData("222a", "0f33", "mi_00")]
    [InlineData("1ff7", "0020", "col03")]
    public void 兜底规则命中(string vid, string pid, string key)
    {
        new McuDevice(string.Format(Template, vid, pid, key)).IsSeewoMcu().Should().BeTrue();
    }

    /// <summary>VID 不在希沃集合里，或接口键不含提示词，都不算。</summary>
    [Theory]
    [InlineData("1234", "5678", "mi_00")]
    [InlineData("222a", "0010", "mi_01")]
    [InlineData("222a", "ffff", "mi_00")]
    public void 非希沃设备不命中(string vid, string pid, string key)
    {
        new McuDevice(string.Format(Template, vid, pid, key)).IsSeewoMcu().Should().BeFalse();
    }

    /// <summary>无法解析的路径不应崩溃，且判定为否。</summary>
    [Fact]
    public void 无法解析的路径()
    {
        var device = new McuDevice(@"\\?\unknown#device");

        device.Vid.Should().Be(-1);
        device.Pid.Should().Be(-1);
        device.IsSeewoMcu().Should().BeFalse();
    }

    /// <summary>必须逐条照抄的定点表共 11 条（文档 §8.2）。</summary>
    [Fact]
    public void 定点表条目数量与文档一致()
    {
        McuDevice.SeewoDevices.Should().HaveCount(11);
    }
}

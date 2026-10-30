using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using EasiLoginer.Domain.Configuration;
using EasiLoginer.Domain.Geometry;
using EasiLoginer.Infrastructure.Configuration;
using EasiLoginer.Infrastructure.Paths;
using FluentAssertions;

namespace EasiLoginer.Infrastructure.Tests;

/// <summary>
/// <c>config.json</c> 的读写与兼容性（文档 §11.1 / §13.2）。
/// </summary>
public sealed class ConfigStoreTests : IDisposable
{
    private readonly string _root;
    private readonly AppPaths _paths;
    private readonly ConfigStore _store;

    public ConfigStoreTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "EasiLoginerTests", "config", Guid.NewGuid().ToString("N"));
        _paths = new AppPaths(_root);
        _store = new ConfigStore(_paths, NullLogger<ConfigStore>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    /// <summary>首次运行：文件不存在时写入默认配置并返回默认值。</summary>
    [Fact]
    public void 文件不存在时创建默认配置()
    {
        var config = _store.Load();

        File.Exists(_paths.ConfigFile).Should().BeTrue();
        config.Login.Method.Should().Be(LoginMethod.Fixed);
        config.App.TelemetryEnabled.Should().BeFalse("EasiLoginer 默认关闭遥测");
        config.Update.Mode.Should().Be(UpdateMode.Never, "首发默认不自动更新");
    }

    /// <summary>写出的 JSON 必须能被自己读回，且关键字段形态与上游一致。</summary>
    [Fact]
    public void 默认配置的JSON形态与上游一致()
    {
        _store.Save(new Config());

        var root = JsonNode.Parse(File.ReadAllText(_paths.ConfigFile))!.AsObject();

        root.ContainsKey("ClassIsland").Should().BeFalse("ClassIsland 段必须移除");
        root["Login"]!["Method"]!.GetValue<int>().Should().Be(0);
        root["Login"]!["Position"]!["AccountLoginTab"]!.AsArray().Select(node => node!.GetValue<int>())
            .Should().Equal(1090, 350);
        root["Banner"]!["Style"]!["TextColor"]!.GetValue<string>().Should().Be("#FFFFDE59");
        root["App"]!["Theme"]!.GetValue<string>().Should().Be("Auto");
        root["Update"]!["TargetUpdateChannel"]!.GetValue<string>().Should().Be("release");
        root["Update"]!["TargetDownloadSource"]!.GetValue<string>().Should().Be("auto");
        root["Statistics"]!.AsObject().ContainsKey("LoginFailureCounts").Should().BeTrue("上游会写出计算字段");
        root["Statistics"]!.AsObject().ContainsKey("ThisInstanceLaunchTime").Should().BeFalse("该字段不落盘");
    }

    /// <summary>警示横幅的自定义文本能落盘并读回。</summary>
    [Fact]
    public void 横幅文本往返()
    {
        var original = _store.Load();
        original.Banner.Style.Text = "自定义提示：课程结束前请勿关闭";
        _store.Save(original);

        var loaded = _store.Load();

        loaded.Banner.Style.Text.Should().Be("自定义提示：课程结束前请勿关闭");
    }

    /// <summary>保存后能无损读回（颜色、坐标、枚举、统计）。</summary>
    [Fact]
    public void 保存后可无损读回()
    {
        var original = new Config();
        original.Login.Method = LoginMethod.Token;
        original.Login.Position.AccountInput = new(1111, 2222);
        original.Banner.Style.TextColor = ArgbColor.Parse("#80112233");
        original.App.Theme = ThemeOptions.Dark;
        original.Update.TargetDownloadSource = DownloadSource.GhProxy;
        original.Statistics.LoginCountsPerAccount["0123456789abcdef"] = 7;

        _store.Save(original);
        var loaded = _store.Load();

        loaded.Login.Method.Should().Be(LoginMethod.Token);
        loaded.Login.Position.AccountInput.Should().Be(new PixelPoint(1111, 2222));
        loaded.Banner.Style.TextColor.Should().Be(ArgbColor.Parse("#80112233"));
        loaded.App.Theme.Should().Be(ThemeOptions.Dark);
        loaded.Update.TargetDownloadSource.Should().Be(DownloadSource.GhProxy);
        loaded.Statistics.LoginCountsPerAccount["0123456789abcdef"].Should().Be(7);
    }

    /// <summary>旧键名必须被迁移，而不是丢弃或报错（§5.1）。</summary>
    [Fact]
    public void 旧键名被迁移()
    {
        WriteJson("""
        {
            "Login": { "ForceEnableScaling": true },
            "Warning": { "Delay": 42 }
        }
        """);

        var config = _store.Load();

        config.Login.ForceCompatibilityMode.Should().BeTrue();
        config.Warning.DelayTime.Should().Be(42);
    }

    /// <summary>旧 config.json 里的 ClassIsland 段与已移除的内部标记必须被丢弃。</summary>
    [Fact]
    public void ClassIsland段被丢弃()
    {
        WriteJson("""
        {
            "ClassIsland": { "AutoPath": true, "DefaultDisplayName": "自动登录希沃白板" },
            "Internal": { "IsAutomationPageNoticeShown": true, "IsOobeCompleted": true }
        }
        """);

        var config = _store.Load();

        config.Internal.IsOobeCompleted.Should().BeTrue();

        // 读取时只忽略、不回写；下一次保存才真正从磁盘消失（§11.2）
        _store.Save(config);

        var rewritten = JsonNode.Parse(File.ReadAllText(_paths.ConfigFile))!.AsObject();
        rewritten.ContainsKey("ClassIsland").Should().BeFalse();
        rewritten["Internal"]!.AsObject().ContainsKey("IsAutomationPageNoticeShown").Should().BeFalse();
    }

    /// <summary>未知字段忽略，缺失字段保留默认值（向前/向后兼容）。</summary>
    [Fact]
    public void 未知字段忽略且缺失字段用默认值()
    {
        WriteJson("""
        {
            "Login": { "Method": 2, "UnknownFutureField": { "a": 1 } },
            "SomeNewSection": [1, 2, 3]
        }
        """);

        var config = _store.Load();

        config.Login.Method.Should().Be(LoginMethod.Uia);
        config.Login.SkipIfLoggedIn.Should().BeTrue();
        config.Login.Timeout.LaunchPollingTimeout.Should().Be(30);
    }

    /// <summary>越界数值被钳制到合法区间，不丢整份配置（§7.2）。</summary>
    [Fact]
    public void 越界数值被钳制()
    {
        WriteJson("""
        {
            "App": { "MaxRetries": 99 },
            "Warning": { "Timeout": 1, "MaxDelays": 9, "DelayTime": 9999 },
            "Banner": { "Style": { "Fps": 0, "TextSpeed": 100 } },
            "Login": { "Timeout": { "LaunchPollingInterval": 0 } }
        }
        """);

        var config = _store.Load();

        config.App.MaxRetries.Should().Be(5);
        config.Warning.Timeout.Should().Be(5);
        config.Warning.MaxDelays.Should().Be(3);
        config.Warning.DelayTime.Should().Be(300);
        config.Banner.Style.Fps.Should().Be(1);
        config.Banner.Style.TextSpeed.Should().Be(8);
        config.Login.Timeout.LaunchPollingInterval.Should().Be(0.1);
    }

    /// <summary>未知枚举值回退默认，不让整份配置失效。</summary>
    [Fact]
    public void 未知枚举值回退默认()
    {
        WriteJson("""
        {
            "App": { "Theme": "Neon" },
            "Update": { "TargetUpdateChannel": "nightly", "TargetDownloadSource": "ftp", "Mode": 77 },
            "Login": { "Method": 42 }
        }
        """);

        var config = _store.Load();

        config.App.Theme.Should().Be(ThemeOptions.Auto);
        config.Update.TargetUpdateChannel.Should().Be(UpdateChannel.Release);
        config.Update.TargetDownloadSource.Should().Be(DownloadSource.Auto);
        config.Update.Mode.Should().Be(UpdateMode.Never);
        config.Login.Method.Should().Be(LoginMethod.Fixed);
    }

    /// <summary>损坏的配置不崩溃：备份为 .bak，用默认值启动（§7.2）。</summary>
    [Fact]
    public void 损坏配置被备份并重置()
    {
        WriteJson("{ this is not json ");

        var config = _store.Load();

        config.App.MaxRetries.Should().Be(2);
        File.Exists(_paths.ConfigFile + ".bak").Should().BeTrue("原文件必须留档");
        File.ReadAllText(_paths.ConfigFile + ".bak").Should().Contain("this is not json");
    }

    /// <summary>空对象等价于全默认。</summary>
    [Fact]
    public void 空对象等价于默认配置()
    {
        WriteJson("{}");

        var config = _store.Load();

        config.Banner.Style.TextColor.Should().Be(ArgbColor.Parse("#FFFFDE59"));
        config.Experimental.PrivacyMask.MaskSize.Should().Be(new PixelPoint(440, 386));
    }

    private void WriteJson(string json)
    {
        Directory.CreateDirectory(_paths.DataDirectory);
        File.WriteAllText(_paths.ConfigFile, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }
}

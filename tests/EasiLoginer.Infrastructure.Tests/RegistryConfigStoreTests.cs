using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Win32;
using EasiLoginer.Domain.Configuration;
using EasiLoginer.Infrastructure.Configuration;
using EasiLoginer.Infrastructure.Paths;
using FluentAssertions;

namespace EasiLoginer.Infrastructure.Tests;

/// <summary>配置默认存注册表，首次升级从旧 config.json 迁移。</summary>
public sealed class RegistryConfigStoreTests : IDisposable
{
    private readonly string _root;
    private readonly string _keyPath;
    private readonly RegistryConfigBackend _backend;

    public RegistryConfigStoreTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "EasiLoginerTests", "registry", Guid.NewGuid().ToString("N"));
        _keyPath = $@"Software\EasiLoginerTests\Config\{Guid.NewGuid():N}";
        _backend = new RegistryConfigBackend(_keyPath);
    }

    public void Dispose()
    {
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(_keyPath, throwOnMissingSubKey: false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
        }

        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    /// <summary>配置能写入注册表并读回。</summary>
    [Fact]
    public void 注册表往返()
    {
        var store = new ConfigStore(new AppPaths(_root), NullLogger<ConfigStore>.Instance, _backend);
        var config = store.Load();
        config.App.Theme = ThemeOptions.Dark;
        config.Login.Timeout.AfterLaunch = 12;
        store.Save(config);

        _backend.Exists.Should().BeTrue();

        using (var key = Registry.CurrentUser.OpenSubKey(_keyPath, writable: false))
        {
            key.Should().NotBeNull();
            key!.GetValueNames().Should().Contain("Login.Method");
            key.GetValueNames().Should().Contain("Appearance.BlurStrength");
            key.GetValue("Login.Method").Should().NotBeNull();
        }

        var reloaded = new ConfigStore(
            new AppPaths(_root),
            NullLogger<ConfigStore>.Instance,
            new RegistryConfigBackend(_keyPath)).Load();

        reloaded.App.Theme.Should().Be(ThemeOptions.Dark);
        reloaded.Login.Timeout.AfterLaunch.Should().Be(12);
    }

    /// <summary>旧版单值 Config 会在首次读取后拆成逐项设置。</summary>
    [Fact]
    public void 旧版单值配置拆分为逐项设置()
    {
        var paths = new AppPaths(_root);
        var defaults = new Config();
        defaults.App.Theme = ThemeOptions.Dark;
        var legacyJson = System.Text.Json.JsonSerializer.Serialize(defaults, ConfigJson.Options);

        using (var key = Registry.CurrentUser.CreateSubKey(_keyPath, writable: true)!)
        {
            key.SetValue(RegistryConfigBackend.LegacyValueName, legacyJson, RegistryValueKind.String);
        }

        var loaded = new ConfigStore(paths, NullLogger<ConfigStore>.Instance, _backend).Load();

        loaded.App.Theme.Should().Be(ThemeOptions.Dark);
        using var upgraded = Registry.CurrentUser.OpenSubKey(_keyPath, writable: false);
        upgraded!.GetValue(RegistryConfigBackend.LegacyValueName).Should().BeNull();
        upgraded.GetValue(RegistryConfigBackend.VersionValueName).Should().Be("2");
        upgraded.GetValue("App.Theme").Should().NotBeNull();
    }

    /// <summary>首次切换到注册表时导入旧 config.json，并保留迁移副本。</summary>
    [Fact]
    public void 旧配置文件迁移到注册表()
    {
        var paths = new AppPaths(_root);
        var fileStore = new ConfigStore(paths, NullLogger<ConfigStore>.Instance, new FileConfigBackend(paths));
        var old = new Config();
        old.App.Theme = ThemeOptions.Light;
        fileStore.Save(old);

        var registryStore = new ConfigStore(paths, NullLogger<ConfigStore>.Instance, _backend);
        var loaded = registryStore.Load();

        loaded.App.Theme.Should().Be(ThemeOptions.Light);
        _backend.Exists.Should().BeTrue();
        File.Exists(paths.ConfigFile).Should().BeFalse("迁移后旧文件改名为 .migrated");
        File.Exists(paths.ConfigFile + ".migrated").Should().BeTrue();
    }
}

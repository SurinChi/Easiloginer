using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using EasiLoginer.Domain.Profiles;
using EasiLoginer.Infrastructure.Paths;
using EasiLoginer.Infrastructure.Profiles;
using EasiLoginer.Infrastructure.Security;
using FluentAssertions;

namespace EasiLoginer.Infrastructure.Tests;

/// <summary>
/// <c>profile.json</c> 的读写、加密与迁移（文档 §5.2 / §7.3 / §11.2）。
/// </summary>
public sealed class ProfileTests : IDisposable
{
    private const string MasterPassword = "主密码 p@ssw0rd";

    private readonly string _root;
    private readonly AppPaths _paths;
    private readonly JsonProfileRepository _repo;

    public ProfileTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "EasiLoginerTests", "profile", Guid.NewGuid().ToString("N"));
        _paths = new AppPaths(_root);
        _repo = NewRepository();
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    /// <summary>首次运行创建明文档案，版本为 3。</summary>
    [Fact]
    public void 首次运行创建明文档案()
    {
        var profile = _repo.Load();

        profile.SchemaVersion.Should().Be(3);
        profile.EncryptionEnabled.Should().BeFalse();
        profile.PasswordlessView.Should().BeTrue();
        profile.Automations.Should().BeEmpty();
        File.Exists(_paths.ProfileFile).Should().BeTrue();
    }

    /// <summary>明文档案的账号密码原样落盘并可读回。</summary>
    [Fact]
    public void 明文档案往返()
    {
        _repo.Load();
        _repo.UpsertAutomation(new AutomationProfile { Name = "测试", Account = "user", Password = "p@ss" });
        _repo.Save();

        File.ReadAllText(_paths.ProfileFile).Should().Contain("\"account\": \"user\"");

        var loaded = NewRepository().Load();

        loaded.Automations.Should().ContainSingle();
        loaded.Automations[0].Account.Should().Be("user");
        loaded.Automations[0].Password.Should().Be("p@ss");
        loaded.Automations[0].Id.Should().NotBeNullOrEmpty();
        loaded.Automations[0].Type.Should().Be("password");
    }

    /// <summary>启用加密后账号密码都变成 ea$ 密文。</summary>
    [Fact]
    public void 启用加密后落盘为密文()
    {
        _repo.Load();
        _repo.UpsertAutomation(new AutomationProfile { Account = "secret-account", Password = "secret-password" });
        _repo.Save();
        _repo.EnableEncryption(MasterPassword);

        var text = File.ReadAllText(_paths.ProfileFile);

        text.Should().Contain(ProfileSchema.SecretPrefix);
        text.Should().NotContain("secret-account");
        text.Should().NotContain("secret-password");
        text.Should().Contain("\"encryption_enabled\": true");
    }

    /// <summary>未解锁时读到的账号密码为空串，解锁后刷新为明文。</summary>
    [Fact]
    public void 锁定态读出空串解锁后恢复明文()
    {
        _repo.Load();
        _repo.UpsertAutomation(new AutomationProfile { Account = "acc", Password = "pw" });
        _repo.Save();
        _repo.EnableEncryption(MasterPassword);

        var locked = NewRepository();
        var profile = locked.Load();

        profile.EncryptionEnabled.Should().BeTrue();
        profile.Automations[0].Account.Should().BeEmpty("锁定态必须静默返回空串");
        profile.Automations[0].Password.Should().BeEmpty();
        locked.IsUnlocked.Should().BeFalse();

        locked.Unlock(MasterPassword).Should().BeTrue();
        locked.IsUnlocked.Should().BeTrue();
        locked.Current.Automations[0].Account.Should().Be("acc");
        locked.Current.Automations[0].Password.Should().Be("pw");
    }

    /// <summary>错误主密码不能解锁。</summary>
    [Fact]
    public void 错误主密码无法解锁()
    {
        _repo.Load();
        _repo.UpsertAutomation(new AutomationProfile { Account = "acc", Password = "pw" });
        _repo.Save();
        _repo.EnableEncryption(MasterPassword);

        var repo = NewRepository();
        repo.Load();

        repo.Unlock("wrong").Should().BeFalse();
        repo.IsUnlocked.Should().BeFalse();
    }

    /// <summary>锁定态保存必须被拒绝，防止把空密码写回磁盘。</summary>
    [Fact]
    public void 锁定态保存被拒绝()
    {
        _repo.Load();
        _repo.UpsertAutomation(new AutomationProfile { Account = "acc", Password = "pw" });
        _repo.Save();
        _repo.EnableEncryption(MasterPassword);

        var locked = NewRepository();
        locked.Load();
        locked.UpsertAutomation(new AutomationProfile { Account = "acc2", Password = "pw2" });

        var action = () => locked.Save();

        action.Should().Throw<ProfileLockedException>();
    }

    /// <summary>DPAPI 缓存可自动解锁；密钥不匹配时清除缓存，缓存损坏时仅拒绝。</summary>
    [Fact]
    public void 缓存解锁与失效处理()
    {
        _repo.Load();
        _repo.UpsertAutomation(new AutomationProfile { Account = "acc", Password = "pw" });
        _repo.Save();
        _repo.EnableEncryption(MasterPassword);

        var cached = NewRepository();
        cached.Load();
        cached.TryCachedUnlock().Should().BeTrue();
        cached.Current.Automations[0].Password.Should().Be("pw");

        // 能解开但与档案校验密文不匹配：必须清除缓存（上游 clear_cached_key 分支）
        new DpapiKeyCache(_paths).Save(RandomNumberGenerator.GetBytes(32));

        var mismatched = NewRepository();
        mismatched.Load();
        mismatched.TryCachedUnlock().Should().BeFalse();
        mismatched.IsUnlocked.Should().BeFalse();
        File.Exists(_paths.UnlockFile).Should().BeFalse("密钥不匹配的缓存必须被清除");

        // 无法解开的缓存：按上游语义仅拒绝，不删除文件
        File.WriteAllBytes(_paths.UnlockFile, [1, 2, 3, 4, 5]);

        var broken = NewRepository();
        broken.Load();
        broken.TryCachedUnlock().Should().BeFalse();
        broken.IsUnlocked.Should().BeFalse();
    }

    /// <summary>关闭加密后恢复明文，并清空缓存与会话密钥。</summary>
    [Fact]
    public void 关闭加密恢复明文()
    {
        _repo.Load();
        _repo.UpsertAutomation(new AutomationProfile { Account = "acc", Password = "pw" });
        _repo.Save();
        _repo.EnableEncryption(MasterPassword);

        _repo.DisableEncryption("wrong").Should().BeFalse();
        _repo.DisableEncryption(MasterPassword).Should().BeTrue();

        var text = File.ReadAllText(_paths.ProfileFile);
        text.Should().NotContain(ProfileSchema.SecretPrefix);
        text.Should().Contain("\"encryption_enabled\": false");
        File.Exists(_paths.UnlockFile).Should().BeFalse();
        _repo.IsUnlocked.Should().BeFalse();

        var reloaded = NewRepository().Load();
        reloaded.Automations[0].Password.Should().Be("pw");
    }

    /// <summary>schema_version &lt; 2 时按上游行为重建空档案。</summary>
    [Fact]
    public void 过旧档案被重建()
    {
        WriteProfile("""
        { "schema_version": 1, "automations": [ { "account": "a", "password": "b" } ] }
        """);

        var profile = _repo.Load();

        profile.Automations.Should().BeEmpty();
        profile.SchemaVersion.Should().Be(3);
        JsonText().Should().Contain("\"schema_version\": 3");
    }

    /// <summary>旧机器密钥加密（ea2$）迁移为明文，成功后写回。</summary>
    [Fact]
    public void 旧机器密钥档案被迁移()
    {
        var legacyKey = NewLegacyKey();
        var token = "ea2$" + new FernetCipher(legacyKey).Encrypt(Encoding.UTF8.GetBytes("legacy-pw"));
        WriteLegacyKey(legacyKey);
        WriteProfile($$"""
        {
            "schema_version": 2,
            "encryption_enabled": true,
            "passwordless_view": true,
            "automations": [
                { "name": "旧档案", "enabled": true, "id": "11111111-1111-1111-1111-111111111111",
                  "account": "legacy-account", "password": "{{token}}" }
            ]
        }
        """);

        var profile = _repo.Load();

        profile.EncryptionEnabled.Should().BeFalse();
        profile.Automations[0].Password.Should().Be("legacy-pw");

        var text = JsonText();
        text.Should().NotContain("ea2$").And.NotContain(ProfileSchema.SecretPrefix);
        text.Should().Contain("\"schema_version\": 3");
    }

    /// <summary>迁移时缺少旧密钥：密码置空、备份原文件、且<b>不</b>写回。</summary>
    [Fact]
    public void 迁移失败时备份且不写回()
    {
        WriteProfile("""
        {
            "schema_version": 2,
            "encryption_enabled": true,
            "automations": [ { "account": "a", "password": "ea2$abcdef" } ]
        }
        """);

        var profile = _repo.Load();

        profile.Automations[0].Password.Should().BeEmpty();
        File.Exists(_paths.ProfileFile + ".bak").Should().BeTrue();
        JsonText().Should().Contain("ea2$abcdef", "迁移失败时不得覆盖原文件");
    }

    /// <summary>账号或密码为空时拒绝保存（上游在序列化阶段抛错）。</summary>
    [Theory]
    [InlineData("", "pw")]
    [InlineData("acc", "")]
    [InlineData("   ", "pw")]
    public void 空账号或密码拒绝保存(string account, string password)
    {
        _repo.Load();
        _repo.UpsertAutomation(new AutomationProfile { Account = account, Password = password });

        var action = () => _repo.Save();

        action.Should().Throw<ProfileValidationException>();
    }

    /// <summary>avatar 上游类型是 Any，可能是对象；读取不能崩，且原样保留。</summary>
    [Fact]
    public void 头像字段容错()
    {
        WriteProfile("""
        {
            "schema_version": 3,
            "automations": [
                { "account": "a", "password": "b", "avatar": { "url": "https://example.com/a.png" } }
            ]
        }
        """);

        var profile = _repo.Load();

        profile.Automations[0].AvatarPath.Should().BeNull();

        _repo.Save();

        JsonText().Should().Contain("\"avatar\": {", "非字符串头像必须原样保留");
    }

    /// <summary>未知字段忽略，不影响读取。</summary>
    [Fact]
    public void 未知字段被忽略()
    {
        WriteProfile("""
        {
            "schema_version": 3,
            "notifier": { "whatever": 1 },
            "automations": [ { "account": "a", "password": "b", "future_field": 42 } ]
        }
        """);

        var profile = _repo.Load();

        profile.Automations.Should().ContainSingle();
        profile.Automations[0].Account.Should().Be("a");
    }

    /// <summary>档案损坏时抛可识别异常而不是随意返回空档案。</summary>
    [Fact]
    public void 损坏档案抛出可识别异常()
    {
        WriteProfile("{ not json ");

        var action = () => NewRepository().Load();

        action.Should().Throw<ProfileLoadException>();
    }

    private JsonProfileRepository NewRepository()
        => new(_paths, NullLogger<JsonProfileRepository>.Instance);

    private string JsonText() => File.ReadAllText(_paths.ProfileFile);

    private void WriteProfile(string json)
    {
        Directory.CreateDirectory(_paths.DataDirectory);
        File.WriteAllText(_paths.ProfileFile, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static byte[] NewLegacyKey() => RandomNumberGenerator.GetBytes(32);

    private void WriteLegacyKey(byte[] key)
    {
        Directory.CreateDirectory(_paths.DataDirectory);
        var text = Convert.ToBase64String(key).Replace('+', '-').Replace('/', '_');
        File.WriteAllText(_paths.LegacyKeyFile, text, Encoding.ASCII);
    }
}

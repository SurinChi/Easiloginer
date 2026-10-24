using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using EasiLoginer.Application.Profiles;
using EasiLoginer.Domain.Profiles;
using EasiLoginer.Infrastructure.Paths;
using EasiLoginer.Infrastructure.Security;

namespace EasiLoginer.Infrastructure.Profiles;

/// <summary>
/// <c>profile.json</c> 的仓储实现：JSON 读写、加密、迁移与解锁会话。
/// </summary>
/// <remarks>
/// <para>与上游 <c>models/profile.py</c> 保持一致的关键行为：</para>
/// <list type="bullet">
///   <item>仅 <c>account</c> / <c>password</c> 两个字段加密，前缀 <c>ea$</c>；</item>
///   <item>锁定态保存直接拒绝（<see cref="ProfileLockedException"/>），避免空密码写回；</item>
///   <item><c>schema_version &lt; 2</c> 重建空档案；<c>&lt; 3</c> 或旧机器密钥加密则归一化为明文；</item>
///   <item>旧档案迁移部分失败时备份 <c>profile.json.bak</c> 且<b>不</b>写回。</item>
/// </list>
/// </remarks>
public sealed class JsonProfileRepository : IProfileRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        IndentSize = 4,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
    };

    private readonly AppPaths _paths;
    private readonly ILogger<JsonProfileRepository> _logger;
    private readonly DpapiKeyCache _keyCache;
    private byte[]? _sessionKey;

    /// <summary>创建档案仓储。</summary>
    public JsonProfileRepository(AppPaths paths, ILogger<JsonProfileRepository> logger)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(logger);

        _paths = paths;
        _logger = logger;
        _keyCache = new DpapiKeyCache(paths);
        Current = new Profile();
    }

    /// <inheritdoc />
    public Profile Current { get; private set; }

    /// <inheritdoc />
    public bool IsUnlocked => _sessionKey is not null;

    /// <inheritdoc />
    public event EventHandler<ProfileChangedEventArgs>? Changed;

    /// <inheritdoc />
    public Profile Load()
    {
        var profilePath = _paths.ProfileFile;

        if (!File.Exists(profilePath))
        {
            Current = new Profile();
            WriteProfile(Current);
            return Current;
        }

        string text;
        try
        {
            text = File.ReadAllText(profilePath, Encoding.UTF8);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new ProfileLoadException("读取档案文件失败。", exception);
        }

        JsonObject root;
        try
        {
            root = JsonNode.Parse(text, nodeOptions: null, documentOptions: new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip,
            }) as JsonObject ?? throw new JsonException("档案根节点必须是 JSON 对象。");
        }
        catch (JsonException exception)
        {
            throw new ProfileLoadException("档案文件解析失败。", exception);
        }

        var version = ReadSchemaVersion(root);

        // 上游行为：过旧或缺失版本号的档案直接重建（激进但需保持）
        if (version < 2)
        {
            _logger.LogWarning("档案 schema_version={Version}，强制重建为空档案", version);
            Current = new Profile();
            WriteProfile(Current);
            return Current;
        }

        if (version < 3 || IsLegacyEncryption(root))
        {
            var migrated = NormalizeLegacyFormat(root);
            root["schema_version"] = ProfileSchema.Version;
            Current = Deserialize(root);

            if (migrated)
            {
                WriteProfile(Current);
                _logger.LogInformation("旧档案已归一化为 schema 3 明文存档");
            }
            else
            {
                BackupOriginal();
                _logger.LogError("旧档案部分密码解密失败，已备份原文件，需重新填写对应密码");
            }

            return Current;
        }

        Current = Deserialize(root);
        return Current;
    }

    /// <inheritdoc />
    public void Save(ProfileChangeReason reason = ProfileChangeReason.ProfileChanged)
    {
        if (Current.EncryptionEnabled && !IsUnlocked)
        {
            throw new ProfileLockedException();
        }

        Validate(Current);
        WriteProfile(Current);
        Changed?.Invoke(this, new ProfileChangedEventArgs(reason));
    }

    /// <inheritdoc />
    public bool Unlock(string masterPassword)
    {
        if (!TryDeriveKey(masterPassword, out var key))
        {
            return false;
        }

        if (!VerifyCheckToken(key))
        {
            CryptographicOperations.ZeroMemory(key);
            return false;
        }

        SetSessionKey(key);
        _keyCache.Save(key);
        Reload();
        return true;
    }

    /// <inheritdoc />
    public bool TryCachedUnlock()
    {
        var key = _keyCache.TryRead();
        if (key is null || key.Length != MasterKeyDeriver.KeyLength)
        {
            return false;
        }

        if (!VerifyCheckToken(key))
        {
            CryptographicOperations.ZeroMemory(key);
            _keyCache.Clear();
            return false;
        }

        SetSessionKey(key);
        Reload();
        return true;
    }

    /// <inheritdoc />
    public void EnableEncryption(string masterPassword)
    {
        var salt = MasterKeyDeriver.CreateSalt();
        var key = MasterKeyDeriver.DeriveRaw(masterPassword, salt);

        SetSessionKey(key);
        _keyCache.Save(key);

        Current.PasswordlessView = true;
        Current.EncryptionSalt = Convert.ToBase64String(salt);

        // EncryptSecretString 自身已加 ea$ 前缀，这里不能再拼一次
        Current.EncryptionCheck = EncryptSecretString(ProfileSchema.MasterPasswordCheckPayload);
        Current.EncryptionEnabled = true;

        Save(ProfileChangeReason.EncryptionChanged);
        _logger.LogInformation("已启用档案加密");
    }

    /// <inheritdoc />
    public bool DisableEncryption(string masterPassword)
    {
        // Unlock 内部会 Reload，保证内存里是明文，避免把锁定态的空密码写回磁盘
        if (!Unlock(masterPassword))
        {
            return false;
        }

        Current.EncryptionEnabled = false;
        Current.EncryptionSalt = null;
        Current.EncryptionCheck = null;

        Save(ProfileChangeReason.EncryptionChanged);
        Lock();
        _keyCache.Clear();
        _logger.LogInformation("已关闭档案加密");
        return true;
    }

    /// <inheritdoc />
    public void Lock()
    {
        if (_sessionKey is not null)
        {
            CryptographicOperations.ZeroMemory(_sessionKey);
            _sessionKey = null;
        }
    }

    /// <inheritdoc />
    public void Reload()
    {
        Load();
        Changed?.Invoke(this, new ProfileChangedEventArgs(ProfileChangeReason.EncryptionChanged));
    }

    /// <inheritdoc />
    public AutomationProfile? FindAutomation(string id)
        => Current.Automations.FirstOrDefault(item => string.Equals(item.Id, id, StringComparison.Ordinal));

    /// <inheritdoc />
    public void UpsertAutomation(AutomationProfile automation)
    {
        ArgumentNullException.ThrowIfNull(automation);

        var index = Current.Automations.FindIndex(item => string.Equals(item.Id, automation.Id, StringComparison.Ordinal));
        if (index >= 0)
        {
            Current.Automations[index] = automation;
            return;
        }

        Current.Automations.Add(automation);
    }

    /// <inheritdoc />
    public bool DeleteAutomation(string id)
    {
        var index = Current.Automations.FindIndex(item => string.Equals(item.Id, id, StringComparison.Ordinal));
        if (index < 0)
        {
            return false;
        }

        Current.Automations.RemoveAt(index);
        return true;
    }

    private static int ReadSchemaVersion(JsonObject root)
    {
        if (root["schema_version"] is not JsonValue value)
        {
            return -1;
        }

        if (value.TryGetValue<int>(out var version))
        {
            return version;
        }

        return value.TryGetValue<double>(out var asDouble) ? (int)asDouble : -1;
    }

    private static bool IsLegacyEncryption(JsonObject root)
    {
        var enabled = root["encryption_enabled"] is JsonValue value &&
                      value.TryGetValue<bool>(out var flag) && flag;

        var hasCheck = root["encryption_check"] is JsonValue check &&
                       check.TryGetValue<string>(out var token) &&
                       !string.IsNullOrEmpty(token);

        return enabled && !hasCheck;
    }

    /// <summary>
    /// 把 <c>schema_version &lt; 3</c> 或旧机器密钥加密的档案归一化为明文。
    /// </summary>
    /// <returns>所有密码都成功解密时返回 true；有失败项时调用方不得写回。</returns>
    private bool NormalizeLegacyFormat(JsonObject root)
    {
        var legacyCipher = TryReadLegacyCipher();
        var success = true;

        if (root["automations"] is JsonArray automations)
        {
            foreach (var element in automations)
            {
                if (element is not JsonObject item)
                {
                    continue;
                }

                if (!item.ContainsKey("type"))
                {
                    item["type"] = "password";
                }

                var token = item["password"] is JsonValue value && value.TryGetValue<string>(out var raw)
                    ? raw
                    : string.Empty;

                // ea2$ 是旧前缀，统一替换为 ea$
                if (token.StartsWith(ProfileSchema.LegacySecretPrefix, StringComparison.Ordinal))
                {
                    token = ProfileSchema.SecretPrefix + token[ProfileSchema.LegacySecretPrefix.Length..];
                }

                if (!token.StartsWith(ProfileSchema.SecretPrefix, StringComparison.Ordinal))
                {
                    item["password"] = token;
                    continue;
                }

                if (legacyCipher is null)
                {
                    item["password"] = string.Empty;
                    success = false;
                    continue;
                }

                try
                {
                    item["password"] = Encoding.UTF8.GetString(legacyCipher.Decrypt(token[ProfileSchema.SecretPrefix.Length..]));
                }
                catch (CryptographicException exception)
                {
                    _logger.LogError(exception, "旧档案密码解密失败，已置空");
                    item["password"] = string.Empty;
                    success = false;
                }
            }
        }

        root["encryption_enabled"] = false;
        root.Remove("encryption_salt");
        root.Remove("encryption_check");
        return success;
    }

    private FernetCipher? TryReadLegacyCipher()
    {
        try
        {
            var keyPath = _paths.LegacyKeyFile;
            if (!File.Exists(keyPath))
            {
                return null;
            }

            var key = File.ReadAllText(keyPath, Encoding.ASCII).Trim();
            return key.Length == 0 ? null : FernetCipher.FromBase64Key(key);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or CryptographicException or FormatException)
        {
            _logger.LogWarning(exception, "读取旧版机器密钥失败");
            return null;
        }
    }

    private Profile Deserialize(JsonObject root)
    {
        var profile = root.Deserialize<Profile>(JsonOptions)
                      ?? throw new ProfileLoadException("档案反序列化结果为空。");

        profile.SchemaVersion = ProfileSchema.Version;

        foreach (var automation in profile.Automations)
        {
            automation.Account = DecryptSecret(automation.Account);
            automation.Password = DecryptSecret(automation.Password);

            if (string.IsNullOrEmpty(automation.Type))
            {
                automation.Type = "password";
            }
        }

        return profile;
    }

    private string DecryptSecret(string value)
    {
        if (string.IsNullOrEmpty(value) ||
            !value.StartsWith(ProfileSchema.SecretPrefix, StringComparison.Ordinal))
        {
            return value;
        }

        // 锁定态：静默返回空串，解锁后由 Reload 刷新为明文（文档 §5.2）
        if (_sessionKey is null)
        {
            return string.Empty;
        }

        try
        {
            var cipher = new FernetCipher(_sessionKey);
            return Encoding.UTF8.GetString(cipher.Decrypt(value[ProfileSchema.SecretPrefix.Length..]));
        }
        catch (CryptographicException exception)
        {
            _logger.LogError(exception, "解密档案字段失败，已置空");
            return string.Empty;
        }
    }

    private string EncryptSecretString(string plaintext)
    {
        if (plaintext.Length == 0)
        {
            return plaintext;
        }

        if (_sessionKey is null)
        {
            throw new ProfileLockedException();
        }

        var cipher = new FernetCipher(_sessionKey);
        return ProfileSchema.SecretPrefix + cipher.Encrypt(Encoding.UTF8.GetBytes(plaintext));
    }

    private void WriteProfile(Profile profile)
    {
        var payload = BuildPayload(profile);
        var json = payload.ToJsonString(JsonOptions);

        Directory.CreateDirectory(_paths.DataDirectory);
        var temporary = _paths.ProfileFile + ".tmp";
        File.WriteAllText(temporary, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        if (File.Exists(_paths.ProfileFile))
        {
            File.Replace(temporary, _paths.ProfileFile, destinationBackupFileName: null);
        }
        else
        {
            File.Move(temporary, _paths.ProfileFile);
        }
    }

    private JsonObject BuildPayload(Profile profile)
    {
        var node = JsonSerializer.SerializeToNode(profile, JsonOptions)!.AsObject();

        if (!profile.EncryptionEnabled)
        {
            return node;
        }

        if (node["automations"] is not JsonArray automations)
        {
            return node;
        }

        foreach (var element in automations)
        {
            if (element is not JsonObject item)
            {
                continue;
            }

            item["account"] = EncryptSecretString(item["account"]?.GetValue<string>() ?? string.Empty);
            item["password"] = EncryptSecretString(item["password"]?.GetValue<string>() ?? string.Empty);
        }

        return node;
    }

    private static void Validate(Profile profile)
    {
        foreach (var automation in profile.Automations)
        {
            if (string.IsNullOrWhiteSpace(automation.Account))
            {
                throw new ProfileValidationException("账号不能为空。");
            }

            if (string.IsNullOrWhiteSpace(automation.Password))
            {
                throw new ProfileValidationException("密码不能为空。");
            }
        }
    }

    private bool TryDeriveKey(string masterPassword, out byte[] key)
    {
        key = [];
        if (string.IsNullOrEmpty(Current.EncryptionSalt))
        {
            return false;
        }

        byte[] salt;
        try
        {
            salt = Convert.FromBase64String(Current.EncryptionSalt);
        }
        catch (FormatException)
        {
            _logger.LogError("档案中的盐值不是合法 Base64，无法解锁");
            return false;
        }

        if (salt.Length != ProfileSchema.SaltLength)
        {
            _logger.LogError("档案中的盐长度非法（{Length}），无法解锁", salt.Length);
            return false;
        }

        key = MasterKeyDeriver.DeriveRaw(masterPassword, salt);
        return true;
    }

    private bool VerifyCheckToken(ReadOnlySpan<byte> key)
    {
        if (string.IsNullOrEmpty(Current.EncryptionCheck) ||
            !Current.EncryptionCheck.StartsWith(ProfileSchema.SecretPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            var cipher = new FernetCipher(key);
            var payload = cipher.Decrypt(Current.EncryptionCheck[ProfileSchema.SecretPrefix.Length..]);
            return payload.AsSpan().SequenceEqual(Encoding.UTF8.GetBytes(ProfileSchema.MasterPasswordCheckPayload));
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    private void SetSessionKey(byte[] key)
    {
        if (_sessionKey is not null)
        {
            CryptographicOperations.ZeroMemory(_sessionKey);
        }

        _sessionKey = key;
    }

    private void BackupOriginal()
    {
        try
        {
            var target = _paths.ProfileFile + ".bak";
            File.Copy(_paths.ProfileFile, target, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(exception, "备份原始档案失败");
        }
    }
}

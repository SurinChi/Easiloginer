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
using EasiLoginer.Infrastructure.Storage;

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
    private List<SensitiveSnapshot> _sensitiveBaseline = [];

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
            CaptureSensitiveBaseline();
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
            CaptureSensitiveBaseline();
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

            CaptureSensitiveBaseline();
            return Current;
        }

        Current = Deserialize(root);
        CaptureSensitiveBaseline();
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
        SealDraftPasswords();
        CaptureSensitiveBaseline();
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

        // 已解锁时的这次调用只是「二次确认主密码」：不要 Reload，
        // 否则会把界面里尚未保存的编辑换成磁盘副本（改完密码再保存会变回旧值）。
        if (IsUnlocked)
        {
            CryptographicOperations.ZeroMemory(key);
            return true;
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

        // 关闭加密前把所有密码密文解密回草稿；任何一条失败都不能写回空密码。
        if (!ReleaseAllPasswords())
        {
            _logger.LogError("关闭加密失败：存在无法解密的密码");
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
    public bool ChangeMasterPassword(string currentMasterPassword, string newMasterPassword)
    {
        if (string.IsNullOrWhiteSpace(newMasterPassword) || !Unlock(currentMasterPassword))
        {
            return false;
        }

        // 旧密钥先把密码解回草稿，再用新密钥重新加密；否则旧密文会留在新密钥下。
        if (!ReleaseAllPasswords())
        {
            _logger.LogError("更换主密码失败：存在无法解密的密码");
            return false;
        }

        var salt = MasterKeyDeriver.CreateSalt();
        var key = MasterKeyDeriver.DeriveRaw(newMasterPassword, salt);

        SetSessionKey(key);
        _keyCache.Save(key);
        Current.EncryptionSalt = Convert.ToBase64String(salt);
        Current.EncryptionCheck = EncryptSecretString(ProfileSchema.MasterPasswordCheckPayload);
        Current.EncryptionEnabled = true;

        Save(ProfileChangeReason.EncryptionChanged);
        _logger.LogInformation("已更换档案主密码");
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

        // 锁定后清掉尚未保存的密码草稿；已保存密码只剩密文，不留在内存。
        if (Current.EncryptionEnabled)
        {
            foreach (var automation in Current.Automations)
            {
                automation.Password = string.Empty;
            }
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
    public bool HasPassword(string id)
        => FindAutomation(id) is { HasStoredPassword: true };

    /// <inheritdoc />
    public bool TryGetPassword(string id, out string password)
    {
        password = string.Empty;

        var automation = FindAutomation(id);
        if (automation is null)
        {
            return false;
        }

        // 用户在界面上新输入的密码草稿优先；已保存密码则按需解密密文。
        if (!string.IsNullOrEmpty(automation.Password))
        {
            password = automation.Password;
            return true;
        }

        if (string.IsNullOrEmpty(automation.EncryptedPassword))
        {
            return true;
        }

        if (_sessionKey is null ||
            !TryDecryptSecret(automation.EncryptedPassword, out var plaintext))
        {
            return false;
        }

        password = plaintext;
        return true;
    }

    /// <inheritdoc />
    public bool VerifyProfilePassword(string id, string candidate)
    {
        var automation = FindAutomation(id);
        if (automation is null || string.IsNullOrEmpty(candidate))
        {
            return false;
        }

        // 修改密码时界面上可能已有新草稿；这里校验的是“原密码”，
        // 因此优先使用已保存密码的密文，而不是新草稿。
        string stored;
        if (!string.IsNullOrEmpty(automation.EncryptedPassword))
        {
            if (!TryDecryptSecret(automation.EncryptedPassword, out stored))
            {
                return false;
            }
        }
        else
        {
            stored = automation.Password;
        }

        if (string.IsNullOrEmpty(stored))
        {
            return false;
        }

        var candidateBytes = Encoding.UTF8.GetBytes(candidate);
        var storedBytes = Encoding.UTF8.GetBytes(stored);

        return candidateBytes.Length == storedBytes.Length &&
               CryptographicOperations.FixedTimeEquals(candidateBytes, storedBytes);
    }

    /// <inheritdoc />
    public void CaptureSensitiveBaseline()
        => _sensitiveBaseline = Current.Automations
            .Select(item => new SensitiveSnapshot(
                item.Id,
                item.Account,
                PasswordFingerprint(item)))
            .ToList();

    /// <inheritdoc />
    public bool HasSensitiveChanges()
    {
        foreach (var automation in Current.Automations)
        {
            var before = _sensitiveBaseline.FirstOrDefault(
                item => string.Equals(item.Id, automation.Id, StringComparison.Ordinal));

            if (before is null)
            {
                if (!string.IsNullOrEmpty(automation.Account) || automation.HasStoredPassword)
                {
                    return true;
                }

                continue;
            }

            if (!string.Equals(before.Account, automation.Account, StringComparison.Ordinal) ||
                !string.Equals(before.Password, PasswordFingerprint(automation), StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 密码基线指纹：新输入草稿与已保存密文都算不同状态，避免切换页面后把编辑值当成基线。
    /// </summary>
    private static string PasswordFingerprint(AutomationProfile automation)
        => !string.IsNullOrEmpty(automation.Password)
            ? "plain:" + automation.Password
            : automation.EncryptedPassword is { Length: > 0 } token
                ? "cipher:" + token
                : string.Empty;

    /// <summary>一条档案的账号/密码基线。</summary>
    private sealed record SensitiveSnapshot(string Id, string Account, string Password);

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

            var rawPassword = automation.Password;
            automation.Password = string.Empty;
            automation.EncryptedPassword = null;

            if (profile.EncryptionEnabled && rawPassword.StartsWith(ProfileSchema.SecretPrefix, StringComparison.Ordinal))
            {
                // 已保存的密码只保留密文，解锁后也不把明文放回档案对象。
                automation.EncryptedPassword = rawPassword;
            }
            else
            {
                // 未加密档案或尚未加密的新密码草稿才留在 Password。
                automation.Password = rawPassword;
            }

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
        return TryDecryptSecret(value, out var plaintext) ? plaintext : string.Empty;
    }

    private bool TryDecryptSecret(string value, out string plaintext)
    {
        plaintext = string.Empty;

        if (string.IsNullOrEmpty(value) ||
            !value.StartsWith(ProfileSchema.SecretPrefix, StringComparison.Ordinal) ||
            _sessionKey is null)
        {
            return false;
        }

        try
        {
            var cipher = new FernetCipher(_sessionKey);
            plaintext = Encoding.UTF8.GetString(cipher.Decrypt(value[ProfileSchema.SecretPrefix.Length..]));
            return true;
        }
        catch (CryptographicException exception)
        {
            _logger.LogError(exception, "解密档案字段失败，已置空");
            return false;
        }
    }

    /// <summary>
    /// 把用户刚输入的新密码草稿加密成密文，并立即清掉密码对象里的明文。
    /// </summary>
    private void SealDraftPasswords()
    {
        if (!Current.EncryptionEnabled)
        {
            return;
        }

        foreach (var automation in Current.Automations)
        {
            if (string.IsNullOrEmpty(automation.Password))
            {
                continue;
            }

            automation.EncryptedPassword = EncryptSecretString(automation.Password);
            automation.Password = string.Empty;
        }
    }

    /// <summary>关闭加密前把全部密文解密回明文草稿；任意一条失败则整体放弃。</summary>
    private bool ReleaseAllPasswords()
    {
        if (_sessionKey is null)
        {
            return false;
        }

        var released = new List<(AutomationProfile Automation, string Plaintext)>();
        foreach (var automation in Current.Automations)
        {
            if (string.IsNullOrEmpty(automation.EncryptedPassword))
            {
                continue;
            }

            if (!TryDecryptSecret(automation.EncryptedPassword, out var plaintext))
            {
                return false;
            }

            released.Add((automation, plaintext));
        }

        foreach (var (automation, plaintext) in released)
        {
            automation.Password = plaintext;
            automation.EncryptedPassword = null;
        }

        return true;
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
        AtomicFile.WriteAllText(_paths.ProfileFile, json);
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

        for (var index = 0; index < automations.Count && index < profile.Automations.Count; index++)
        {
            if (automations[index] is not JsonObject item)
            {
                continue;
            }

            var automation = profile.Automations[index];
            item["account"] = EncryptSecretString(item["account"]?.GetValue<string>() ?? string.Empty);

            // 新密码草稿优先；否则沿用已保存密文，绝不再加密一次。
            item["password"] = !string.IsNullOrEmpty(automation.Password)
                ? EncryptSecretString(automation.Password)
                : automation.EncryptedPassword ?? string.Empty;
        }

        return node;
    }

    private static void Validate(Profile profile)
    {
        foreach (var automation in profile.Automations)
        {
            if (string.IsNullOrWhiteSpace(automation.Account))
            {
                throw new ProfileValidationException($"档案「{automation.DisplayLabel}」的账号不能为空。");
            }

            // 密码允许留空：登录前会再校验，避免用户必须先拿到密码才能建档案。
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

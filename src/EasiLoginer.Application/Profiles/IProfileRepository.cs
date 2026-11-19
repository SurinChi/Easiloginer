using EasiLoginer.Domain.Profiles;

namespace EasiLoginer.Application.Profiles;

/// <summary>
/// 档案仓储端口：读写档案、管理加密状态与解锁会话。
/// </summary>
/// <remarks>
/// 实现位于基础设施层（<c>JsonProfileRepository</c>），负责 Argon2id + Fernet + DPAPI
/// 与 JSON 迁移；应用层与呈现层只依赖本接口，不接触密钥材料（文档 §6.1）。
/// </remarks>
public interface IProfileRepository
{
    /// <summary>当前档案（加密且未解锁时，账号密码为空串）。</summary>
    Profile Current { get; }

    /// <summary>当前会话是否已解锁（未启用加密时视为已解锁）。</summary>
    bool IsUnlocked { get; }

    /// <summary>档案变更事件。</summary>
    event EventHandler<ProfileChangedEventArgs>? Changed;

    /// <summary>从磁盘载入档案；文件不存在时创建默认档案。</summary>
    Profile Load();

    /// <summary>保存档案；加密且未解锁时抛 <see cref="ProfileLockedException"/>。</summary>
    void Save(ProfileChangeReason reason = ProfileChangeReason.ProfileChanged);

    /// <summary>校验主密码并解锁当前会话。</summary>
    bool Unlock(string masterPassword);

    /// <summary>尝试用 DPAPI 缓存自动解锁；缓存失效时清除并返回 false。</summary>
    bool TryCachedUnlock();

    /// <summary>启用主密码加密并立即落盘。</summary>
    void EnableEncryption(string masterPassword);

    /// <summary>校验主密码后关闭加密；密码错误返回 false。</summary>
    bool DisableEncryption(string masterPassword);

    /// <summary>校验旧主密码后更换主密码；密码错误或无法重新加密返回 false。</summary>
    bool ChangeMasterPassword(string currentMasterPassword, string newMasterPassword);

    /// <summary>清除会话密钥。</summary>
    void Lock();

    /// <summary>重新从磁盘载入（解锁后刷新明文密码）。</summary>
    void Reload();

    /// <summary>记录当前账号/密码为“已保存”基线，用于跨页面判断敏感改动。</summary>
    void CaptureSensitiveBaseline();

    /// <summary>
    /// 当前内存里的账号/密码是否与上次载入/保存不同。
    /// </summary>
    /// <remarks>
    /// 基线保存在仓储上而不是页面视图模型里，这样切换页面重建视图模型后，
    /// 仍然能发现「改了账号/密码但还没保存」并重新要求验证主密码。
    /// </remarks>
    bool HasSensitiveChanges();

    /// <summary>按 ID 查找档案。</summary>
    AutomationProfile? FindAutomation(string id);

    /// <summary>档案是否已保存密码（不判断空串密码）。</summary>
    bool HasPassword(string id);

    /// <summary>取出档案密码供登录/校验使用；锁定态或解密失败返回 false。</summary>
    bool TryGetPassword(string id, out string password);

    /// <summary>校验档案原密码；用于修改账号/密码时的备选验证。</summary>
    bool VerifyProfilePassword(string id, string candidate);

    /// <summary>新增或按 ID 覆盖一条档案（不落盘）。</summary>
    void UpsertAutomation(AutomationProfile automation);

    /// <summary>按 ID 删除档案。</summary>
    bool DeleteAutomation(string id);
}

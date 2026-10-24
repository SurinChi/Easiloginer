namespace EasiLoginer.Domain.Profiles;

/// <summary>档案格式常量，对应上游 <c>models/profile.py</c> 顶部的私有常量。</summary>
public static class ProfileSchema
{
    /// <summary>当前档案格式版本。</summary>
    public const int Version = 3;

    /// <summary>密文字段前缀。</summary>
    public const string SecretPrefix = "ea$";

    /// <summary>旧版密文字段前缀（仅迁移时出现）。</summary>
    public const string LegacySecretPrefix = "ea2$";

    /// <summary>主密码校验密文中保存的固定负载。</summary>
    public const string MasterPasswordCheckPayload = "EasiAuto-profile-master-check";

    /// <summary>主密码盐长度（字节）。</summary>
    public const int SaltLength = 16;
}

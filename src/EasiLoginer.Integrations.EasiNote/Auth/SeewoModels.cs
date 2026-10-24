using System.Text.Json;

namespace EasiLoginer.Integrations.EasiNote.Auth;

/// <summary>登录响应中的 <c>user</c> 字段。</summary>
public sealed record UserInfo
{
    /// <summary>用户 ID。</summary>
    public string Uid { get; init; } = string.Empty;

    /// <summary>用户名。</summary>
    public string Username { get; init; } = string.Empty;

    /// <summary>昵称（<c>nickName</c> 优先，回退 <c>nickname</c>）。</summary>
    public string NickName { get; init; } = string.Empty;

    /// <summary>手机号。</summary>
    public string Phone { get; init; } = string.Empty;

    /// <summary>邮箱。</summary>
    public string Email { get; init; } = string.Empty;

    /// <summary>头像地址。</summary>
    public string PhotoUrl { get; init; } = string.Empty;

    /// <summary>账号 ID。</summary>
    public string AccountId { get; init; } = string.Empty;

    /// <summary>真实姓名。</summary>
    public string RealName { get; init; } = string.Empty;

    /// <summary>单位 ID。</summary>
    public string UnitId { get; init; } = string.Empty;

    /// <summary>账号类型。</summary>
    public int AccountType { get; init; }

    /// <summary>性别。</summary>
    public int Gender { get; init; }

    /// <summary>从响应解析。</summary>
    public static UserInfo FromResponse(JsonElement element)
    {
        var user = element.TryGetProperty("user", out var nested) ? nested : element;

        return new UserInfo
        {
            Uid = ReadString(user, "uid"),
            Username = ReadString(user, "username"),
            NickName = ReadString(user, "nickName") is { Length: > 0 } nick ? nick : ReadString(user, "nickname"),
            Phone = ReadString(user, "phone"),
            Email = ReadString(user, "email"),
            PhotoUrl = ReadString(user, "photoUrl"),
            AccountId = ReadString(user, "accountId"),
            RealName = ReadString(user, "realName"),
            UnitId = ReadString(user, "unitId"),
            AccountType = ReadInt32(user, "accountType"),
            Gender = ReadInt32(user, "gender"),
        };
    }

    internal static string ReadString(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
        {
            return string.Empty;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
            _ => value.GetRawText(),
        };
    }

    internal static int ReadInt32(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)
            ? number
            : 0;
}

/// <summary>登录成功时的完整结果。</summary>
/// <param name="Token">登录令牌。</param>
/// <param name="User">用户信息。</param>
/// <param name="IsRegister">是否新注册。</param>
public sealed record LoginResult(string Token, UserInfo User, int IsRegister)
{
    /// <summary>从响应解析（兼容 <c>data</c> 包裹与平铺两种结构）。</summary>
    public static LoginResult FromResponse(JsonElement response)
    {
        var inner = response.TryGetProperty("data", out var data) ? data : response;

        return new LoginResult(
            Token: UserInfo.ReadString(inner, "token"),
            User: UserInfo.FromResponse(inner),
            IsRegister: UserInfo.ReadInt32(inner, "isRegister"));
    }
}

/// <summary><c>GET /api/v2/user/info</c> 返回的用户详情。</summary>
public sealed record UserProfile
{
    /// <summary>头像地址。</summary>
    public string PhotoUrl { get; init; } = string.Empty;

    /// <summary>动态头像地址。</summary>
    public string AnimatedPhoto { get; init; } = string.Empty;

    /// <summary>手机号。</summary>
    public string Phone { get; init; } = string.Empty;

    /// <summary>邮箱。</summary>
    public string Email { get; init; } = string.Empty;

    /// <summary>昵称。</summary>
    public string NickName { get; init; } = string.Empty;

    /// <summary>真实姓名。</summary>
    public string RealName { get; init; } = string.Empty;

    /// <summary>头像 URL：优先 <c>animatedUserPhoto</c>，其次 <c>photoUrl</c>。</summary>
    public string? Avatar => !string.IsNullOrEmpty(AnimatedPhoto)
        ? AnimatedPhoto
        : string.IsNullOrEmpty(PhotoUrl) ? null : PhotoUrl;

    /// <summary>从响应解析。</summary>
    public static UserProfile FromResponse(JsonElement response)
    {
        var data = response.TryGetProperty("data", out var inner) ? inner : response;
        var extended = data.TryGetProperty("userInfoExtendVo", out var extend) && extend.ValueKind == JsonValueKind.Object
            ? extend
            : default;

        return new UserProfile
        {
            PhotoUrl = UserInfo.ReadString(data, "photoUrl"),
            AnimatedPhoto = extended.ValueKind == JsonValueKind.Object ? UserInfo.ReadString(extended, "animatedUserPhoto") : string.Empty,
            Phone = UserInfo.ReadString(data, "phone"),
            Email = UserInfo.ReadString(data, "email"),
            NickName = UserInfo.ReadString(data, "nickName"),
            RealName = UserInfo.ReadString(data, "realName"),
        };
    }
}

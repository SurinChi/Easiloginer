namespace EasiLoginer.Domain.Automation;

/// <summary>希沃白板当前登录态（来自命名管道查询）。</summary>
/// <param name="StatusCode">状态码，202 表示已登录。</param>
/// <param name="UserId">当前登录用户 ID。</param>
/// <param name="Token">当前令牌。</param>
public sealed record EasiNoteLoginState(int StatusCode, string UserId, string Token)
{
    /// <summary>白板当前是否处于已登录状态。</summary>
    public bool IsLoggedIn => StatusCode == 202;
}

/// <summary>希沃账号信息。</summary>
/// <param name="Uid">用户 ID。</param>
/// <param name="Username">用户名。</param>
/// <param name="NickName">昵称。</param>
/// <param name="Phone">手机号。</param>
public sealed record SeewoUser(string Uid, string Username, string NickName, string Phone);

/// <summary>一次希沃 API 登录的会话结果。</summary>
/// <param name="Token">登录令牌。</param>
/// <param name="User">用户信息。</param>
/// <param name="IsRegister">是否新注册。</param>
public sealed record SeewoSession(string Token, SeewoUser User, int IsRegister);

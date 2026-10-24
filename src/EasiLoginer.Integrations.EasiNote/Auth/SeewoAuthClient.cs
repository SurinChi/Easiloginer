using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace EasiLoginer.Integrations.EasiNote.Auth;

/// <summary>
/// 希沃账号服务客户端，对应上游 <c>integrations/easinote/api.py</c>。
/// </summary>
/// <remarks>
/// <para>协议固定项：</para>
/// <list type="bullet">
///   <item>Base <c>https://edu.seewo.com</c>，登录 <c>POST /api/v1/auth/login</c>，用户信息 <c>GET /api/v2/user/info</c>；</item>
///   <item>Cookie <c>x-auth-app=EasiNote5</c>（可选 <c>x-auth-brand</c>），UA <c>EasiNote/5.2.4</c>；</item>
///   <item>密码按协议要求做 MD5（<b>不是</b>安全存储，只在内存中短暂存在）；</item>
///   <item>错误码 4906 → 需要验证码；4908/4909 → 账号或密码错误；其余 → 通用失败。</item>
/// </list>
/// </remarks>
public sealed class SeewoAuthClient
{
    /// <summary>默认服务地址。</summary>
    public const string DefaultBaseUrl = "https://edu.seewo.com";

    /// <summary>默认应用标识。</summary>
    public const string DefaultAppCode = "EasiNote5";

    /// <summary>User-Agent（与希沃白板一致）。</summary>
    public const string UserAgent = "EasiNote/5.2.4";

    private readonly HttpClient _http;
    private readonly string _baseUrl;
    private readonly string _appCode;
    private readonly string _appBrand;

    /// <summary>创建客户端。</summary>
    public SeewoAuthClient(
        HttpClient httpClient,
        string baseUrl = DefaultBaseUrl,
        string appCode = DefaultAppCode,
        string appBrand = "")
    {
        ArgumentNullException.ThrowIfNull(httpClient);

        _http = httpClient;
        _baseUrl = baseUrl.TrimEnd('/');
        _appCode = appCode;
        _appBrand = appBrand;
    }

    /// <summary>按协议对密码做 MD5（小写十六进制），对应 C# <c>SecurityUtility.Encrypt32()</c>。</summary>
    [SuppressMessage(
        "Security",
        "CA5351:Do Not Use Broken Cryptographic Algorithms",
        Justification = "希沃登录协议要求 MD5，这里只做协议编码，不用于存储或校验口令。")]
    public static string HashPassword(string plainPassword)
    {
        ArgumentNullException.ThrowIfNull(plainPassword);

        var hash = MD5.HashData(Encoding.UTF8.GetBytes(plainPassword));
        return Convert.ToHexStringLower(hash);
    }

    /// <summary>账号密码登录。</summary>
    /// <exception cref="SeewoCaptchaException">需要图形验证码。</exception>
    /// <exception cref="SeewoAuthException">账号或密码错误。</exception>
    /// <exception cref="SeewoLoginException">其它业务失败。</exception>
    /// <exception cref="SeewoNetworkException">网络失败（可重试）。</exception>
    public async Task<LoginResult> LoginAsync(
        string username,
        string password,
        string? captchaKey = null,
        string? captchaContent = null,
        string? phoneCountryCode = null,
        CancellationToken cancellationToken = default)
    {
        var body = new JsonObject
        {
            ["username"] = username,
            ["password"] = HashPassword(password),
        };

        if (!string.IsNullOrEmpty(captchaKey) && !string.IsNullOrEmpty(captchaContent))
        {
            body["captcha"] = new JsonObject
            {
                ["key"] = captchaKey,
                ["captcha"] = captchaContent,
            };
        }

        if (!string.IsNullOrEmpty(phoneCountryCode))
        {
            body["phoneCountryCode"] = phoneCountryCode;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/api/v1/auth/login")
        {
            Content = JsonContent.Create(body),
        };
        ApplyCommonHeaders(request, cookie: BuildCookie());

        JsonElement response;
        try
        {
            using var message = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            message.EnsureSuccessStatusCode();

            await using var stream = await message.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            response = document.RootElement.Clone();
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            throw new SeewoNetworkException($"网络请求失败：{exception.Message}", exception);
        }

        var errorCode = response.TryGetProperty("error_code", out var code) && code.ValueKind == JsonValueKind.Number
            ? code.GetInt32()
            : -1;

        if (errorCode != 0)
        {
            throw CreateError(errorCode, response.GetRawText());
        }

        return LoginResult.FromResponse(response);
    }

    /// <summary>通过 token 获取用户详情（含头像）；失败返回 <see langword="null"/>。</summary>
    public async Task<UserProfile?> GetUserProfileAsync(string token, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{_baseUrl}/api/v2/user/info");
        ApplyCommonHeaders(request, cookie: $"{BuildCookie()}; x-auth-token={token}");

        try
        {
            using var message = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!message.IsSuccessStatusCode)
            {
                return null;
            }

            await using var stream = await message.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);

            return UserProfile.FromResponse(document.RootElement);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            return null;
        }
    }

    private string BuildCookie()
        => string.IsNullOrEmpty(_appBrand)
            ? $"x-auth-app={_appCode}"
            : $"x-auth-app={_appCode}; x-auth-brand={_appBrand}";

    private static void ApplyCommonHeaders(HttpRequestMessage request, string cookie)
    {
        request.Headers.TryAddWithoutValidation("Cookie", cookie);
        request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
        request.Headers.TryAddWithoutValidation("Accept", "application/json");
    }

    private static SeewoLoginException CreateError(int errorCode, string body)
    {
        var message = "未知错误";
        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("message", out var value) && value.ValueKind == JsonValueKind.String)
            {
                message = value.GetString() ?? message;
            }
        }
        catch (JsonException)
        {
            // 保持默认消息
        }

        return errorCode switch
        {
            4906 => new SeewoCaptchaException(message, body),
            4908 or 4909 => new SeewoAuthException($"账号或密码错误 ({errorCode})", errorCode, body),
            _ => new SeewoLoginException($"登录失败 [{errorCode}]：{message}", errorCode, body),
        };
    }
}

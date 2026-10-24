using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace EasiLoginer.Integrations.EasiNote.Pipe;

/// <summary>投递给希沃白板的登录令牌载荷，字段名与 SeewoPipeBridge 约定一致。</summary>
/// <param name="Token">登录令牌。</param>
/// <param name="UserId">用户 ID。</param>
/// <param name="UserName">用户名。</param>
/// <param name="NickName">昵称。</param>
/// <param name="Phone">手机号。</param>
/// <param name="StatusCode">状态码，默认 202。</param>
/// <param name="Result">结果地址，默认 <c>https://e.seewo.com</c>。</param>
/// <param name="Message">提示文案。</param>
public sealed record TokenPayload(
    string Token,
    string UserId,
    string UserName,
    string NickName,
    string Phone,
    int StatusCode = 202,
    string Result = "https://e.seewo.com",
    string Message = "客户端已扫码并确认登录")
{
    /// <summary>序列化为单行 JSON（字段顺序与上游一致）。</summary>
    public string ToJson()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            Indented = false,
        }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("statusCode", StatusCode);
            writer.WriteString("token", Token);
            writer.WriteString("userId", UserId);
            writer.WriteString("userName", UserName);
            writer.WriteString("nickName", NickName);
            writer.WriteString("phone", Phone);
            writer.WriteString("result", Result);
            writer.WriteString("message", Message);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }
}

/// <summary>白板对令牌投递的回执。</summary>
/// <param name="Success">是否成功。</param>
/// <param name="Message">消息。</param>
/// <param name="ErrorDetail">错误详情。</param>
/// <param name="Code">错误码。</param>
public sealed record TokenResponse(bool Success, string Message, string ErrorDetail, string? Code)
{
    /// <summary>回执是否表示令牌已失效（需重新获取令牌）。</summary>
    public bool TokenExpired =>
        string.Equals(Code, "4000", StringComparison.Ordinal) ||
        (ErrorDetail + Message).Contains("4000", StringComparison.Ordinal);

    /// <summary>解析回执行。</summary>
    /// <exception cref="PipeUnavailableException">回执不是合法 JSON 对象。</exception>
    public static TokenResponse Parse(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            throw new PipeUnavailableException("管道未返回响应。");
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(line);
        }
        catch (JsonException exception)
        {
            throw new PipeUnavailableException($"回执不是合法 JSON：{line}", exception);
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new PipeUnavailableException($"回执不是 JSON 对象：{line}");
            }

            return new TokenResponse(
                Success: root.TryGetProperty(nameof(Success), out var success) && success.ValueKind == JsonValueKind.True,
                Message: GetString(root, "Message"),
                ErrorDetail: GetString(root, "ErrorDetail"),
                Code: GetCode(root));
        }
    }

    private static string GetString(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value))
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

    private static string? GetCode(JsonElement root)
    {
        foreach (var name in new[] { "Code", "ErrorCode", "errorCode" })
        {
            if (!root.TryGetProperty(name, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            {
                continue;
            }

            return value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText();
        }

        return null;
    }
}

/// <summary>白板当前登录态（<c>SeewoLoginInfoPipe</c> 的返回值）。</summary>
/// <param name="StatusCode">状态码，202 表示已登录。</param>
/// <param name="UserId">用户 ID。</param>
/// <param name="Token">令牌。</param>
/// <param name="UserName">用户名。</param>
/// <param name="NickName">昵称。</param>
/// <param name="Phone">手机号。</param>
/// <param name="Message">消息。</param>
public sealed record CurrentLoginInfo(
    int StatusCode,
    string UserId,
    string Token,
    string UserName,
    string NickName,
    string Phone,
    string Message)
{
    /// <summary>白板当前是否处于已登录状态。</summary>
    public bool IsLoggedIn => StatusCode == 202;

    /// <summary>解析响应行；结构非法时返回 <see langword="null"/>（与上游一致，不抛异常）。</summary>
    public static CurrentLoginInfo? TryParse(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(line);
        }
        catch (JsonException)
        {
            return null;
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (!TryGetStatusCode(root, out var statusCode))
            {
                return null;
            }

            return new CurrentLoginInfo(
                StatusCode: statusCode,
                UserId: ReadString(root, "userId"),
                Token: ReadString(root, "token"),
                UserName: ReadString(root, "userName"),
                NickName: ReadString(root, "nickName"),
                Phone: ReadString(root, "phone"),
                Message: ReadString(root, "message"));
        }
    }

    private static bool TryGetStatusCode(JsonElement root, out int statusCode)
    {
        statusCode = 0;
        if (!root.TryGetProperty("statusCode", out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        switch (value.ValueKind)
        {
            case JsonValueKind.Number:
                if (value.TryGetInt32(out statusCode))
                {
                    return true;
                }

                return false;
            case JsonValueKind.String:
                return int.TryParse(value.GetString(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out statusCode);
            default:
                return false;
        }
    }

    private static string ReadString(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value))
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
}

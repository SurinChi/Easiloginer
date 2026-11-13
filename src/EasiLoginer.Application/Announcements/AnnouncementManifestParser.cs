using EasiLoginer.Domain.Announcements;

namespace EasiLoginer.Application.Announcements;

/// <summary>
/// 公告清单来源。返回原始 JSON 文本，<see langword="null"/> 表示「没有公告源 / 没有公告」。
/// </summary>
/// <remarks>
/// 实现放在基础设施层（本地文件或 HTTP），应用层只负责解析、过滤与展示决策。
/// </remarks>
public interface IAnnouncementSource
{
    /// <summary>读取公告清单原文。</summary>
    Task<string?> FetchAsync(CancellationToken cancellationToken);
}

/// <summary>
/// 公告清单解析器：把发布方上传的 JSON 文本转成 <see cref="Announcement"/> 列表。
/// </summary>
/// <remarks>
/// 接受的根节点：<c>{"announcements":[...]}</c> 或直接一个数组。
/// 单条公告缺 <c>id</c> 或 <c>title</c>、或超出长度上限时整条跳过——
/// 宁可少显示一条，也不要因为一条坏数据让整个公告位变成一片空白或崩溃。
/// </remarks>
public static class AnnouncementManifestParser
{
    /// <summary>单次最多解析多少条（防止异常文件拖慢启动）。</summary>
    public const int MaxItems = 50;

    /// <summary>字段长度上限。</summary>
    public const int MaxIdLength = 128;

    /// <summary>标题长度上限。</summary>
    public const int MaxTitleLength = 200;

    /// <summary>正文长度上限。</summary>
    public const int MaxBodyLength = 4000;

    /// <summary>链接长度上限。</summary>
    public const int MaxUrlLength = 2048;

    /// <summary>解析公告清单。</summary>
    /// <exception cref="FormatException">根节点不是对象或数组，或对象缺少 announcements 数组。</exception>
    /// <exception cref="System.Text.Json.JsonException">文本不是合法 JSON。</exception>
    public static IReadOnlyList<Announcement> Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        using var document = System.Text.Json.JsonDocument.Parse(json, new System.Text.Json.JsonDocumentOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = System.Text.Json.JsonCommentHandling.Skip,
        });

        var root = document.RootElement;
        System.Text.Json.JsonElement items;

        switch (root.ValueKind)
        {
            case System.Text.Json.JsonValueKind.Array:
                items = root;
                break;

            case System.Text.Json.JsonValueKind.Object
                when root.TryGetProperty("announcements", out var array) &&
                     array.ValueKind == System.Text.Json.JsonValueKind.Array:
                items = array;
                break;

            case System.Text.Json.JsonValueKind.Object:
                throw new FormatException("公告清单缺少 announcements 数组。");

            default:
                throw new FormatException("公告清单根节点必须是对象或数组。");
        }

        var result = new List<Announcement>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var item in items.EnumerateArray())
        {
            if (result.Count >= MaxItems)
            {
                break;
            }

            var announcement = ParseItem(item);
            if (announcement is null || !seen.Add(announcement.Id))
            {
                continue;
            }

            result.Add(announcement);
        }

        return result;
    }

    private static Announcement? ParseItem(System.Text.Json.JsonElement item)
    {
        if (item.ValueKind != System.Text.Json.JsonValueKind.Object)
        {
            return null;
        }

        var id = ReadString(item, "id");
        var title = ReadString(item, "title");

        if (string.IsNullOrWhiteSpace(id) || id.Length > MaxIdLength)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(title) || title.Length > MaxTitleLength)
        {
            return null;
        }

        var publishedAt = ReadTime(item, "published_at") ?? ReadTime(item, "start_at");
        if (publishedAt is null)
        {
            return null;
        }

        var body = ReadString(item, "body") ?? string.Empty;
        if (body.Length > MaxBodyLength)
        {
            return null;
        }

        return new Announcement(
            id,
            title,
            body,
            ParseSeverity(ReadString(item, "severity")),
            publishedAt.Value,
            ReadTime(item, "start_at"),
            ReadTime(item, "end_at"),
            ReadString(item, "min_version"),
            ReadString(item, "max_version"),
            ReadUrl(item),
            ReadBool(item, "dismissible") ?? true);
    }

    private static string? ReadString(System.Text.Json.JsonElement item, string name)
        => item.TryGetProperty(name, out var value) && value.ValueKind == System.Text.Json.JsonValueKind.String
            ? value.GetString()?.Trim()
            : null;

    private static bool? ReadBool(System.Text.Json.JsonElement item, string name)
    {
        if (!item.TryGetProperty(name, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            System.Text.Json.JsonValueKind.True => true,
            System.Text.Json.JsonValueKind.False => false,
            _ => null,
        };
    }

    private static DateTimeOffset? ReadTime(System.Text.Json.JsonElement item, string name)
    {
        var text = ReadString(item, name);
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return DateTimeOffset.TryParse(
            text,
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeUniversal,
            out var value)
            ? value
            : null;
    }

    private static string? ReadUrl(System.Text.Json.JsonElement item)
    {
        var text = ReadString(item, "url");
        if (string.IsNullOrWhiteSpace(text) || text.Length > MaxUrlLength)
        {
            return null;
        }

        return Uri.TryCreate(text, UriKind.Absolute, out var uri) &&
               (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            ? uri.AbsoluteUri
            : null;
    }

    private static AnnouncementSeverity ParseSeverity(string? text)
        => text?.ToLowerInvariant() switch
        {
            "warning" => AnnouncementSeverity.Warning,
            "critical" or "important" => AnnouncementSeverity.Critical,
            _ => AnnouncementSeverity.Info,
        };
}

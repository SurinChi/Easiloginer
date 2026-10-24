using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EasiLoginer.Infrastructure.Configuration;

/// <summary>
/// <c>config.json</c> 的序列化约定。
/// </summary>
/// <remarks>
/// 与上游对齐：UTF-8、4 空格缩进、不转义中文（对应 <c>ensure_ascii=False</c>）、
/// 字段名由 <c>[JsonPropertyName]</c> 固定，不依赖命名策略（文档 §4.4 / §7.2）。
/// </remarks>
internal static class ConfigJson
{
    /// <summary>共享的序列化选项。</summary>
    public static JsonSerializerOptions Options { get; } = new()
    {
        WriteIndented = true,
        IndentSize = 4,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
        Converters =
        {
            new PixelPointJsonConverter(),
            new ArgbColorJsonConverter(),
        },
    };

    /// <summary>解析 <c>JsonNode</c> 时使用的宽松选项。</summary>
    public static JsonDocumentOptions DocumentOptions => new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };
}

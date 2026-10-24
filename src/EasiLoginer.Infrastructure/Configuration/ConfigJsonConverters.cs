using System.Text.Json;
using System.Text.Json.Serialization;
using EasiLoginer.Domain.Configuration;
using EasiLoginer.Domain.Geometry;

namespace EasiLoginer.Infrastructure.Configuration;

/// <summary>把 <see cref="PixelPoint"/> 读写为 <c>[x, y]</c> 数组（与上游元组写法一致）。</summary>
internal sealed class PixelPointJsonConverter : JsonConverter<PixelPoint>
{
    public override PixelPoint Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return default;
        }

        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException("坐标必须是 [x, y] 数组。");
        }

        reader.Read();
        var x = ReadCoordinate(ref reader);
        reader.Read();
        var y = ReadCoordinate(ref reader);
        reader.Read();

        if (reader.TokenType != JsonTokenType.EndArray)
        {
            throw new JsonException("坐标必须恰好包含两个数字。");
        }

        return new PixelPoint(x, y);
    }

    public override void Write(Utf8JsonWriter writer, PixelPoint value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        writer.WriteNumberValue(value.X);
        writer.WriteNumberValue(value.Y);
        writer.WriteEndArray();
    }

    private static int ReadCoordinate(ref Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.Number)
        {
            throw new JsonException("坐标元素必须是数字。");
        }

        return reader.TryGetInt32(out var value) ? value : (int)reader.GetDouble();
    }
}

/// <summary>把 <see cref="ArgbColor"/> 读写为 <c>#AARRGGBB</c> 字符串。</summary>
internal sealed class ArgbColorJsonConverter : JsonConverter<ArgbColor>
{
    public override ArgbColor Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return default;
        }

        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException("颜色必须是 #AARRGGBB 字符串。");
        }

        return ArgbColor.TryParse(reader.GetString(), out var color) ? color : default;
    }

    public override void Write(Utf8JsonWriter writer, ArgbColor value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString());
}

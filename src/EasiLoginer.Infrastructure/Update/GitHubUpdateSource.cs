using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using EasiLoginer.Application.Update;
using EasiLoginer.Domain.Configuration;

namespace EasiLoginer.Infrastructure.Update;

/// <summary>
/// 用 GitHub Releases 作为更新源（文档 §12.4：只消费本项目自己的清单）。
/// </summary>
/// <remarks>
/// <para>Stable 通道取 <c>/releases/latest</c>（GitHub 保证不含预发布）；
/// Dev 通道取 <c>/releases?per_page=1</c> 的最新一条（可能是预发布）。</para>
/// <para>GitHub 的发布对象不是本项目的清单格式，这里把它转换成
/// <c>{ version, channel, notes, assets[] }</c> 再交给 <c>UpdateChecker</c>，
/// 这样版本比较与通道校验逻辑保持不变。</para>
/// <para>没有发布（404）返回 <see langword="null"/>；网络/格式问题按端口约定抛
/// <see cref="IOException"/>（Application 层不引用 <c>System.Net.Http</c>）。</para>
/// </remarks>
public sealed class GitHubUpdateSource : IUpdateSource
{
    private const string ApiRoot = "https://api.github.com/repos";

    private readonly HttpClient _http;
    private readonly string _owner;
    private readonly string _repository;

    /// <summary>创建更新源。</summary>
    /// <param name="http">HTTP 客户端（由调用方管理生命周期）。</param>
    /// <param name="owner">仓库所有者。</param>
    /// <param name="repository">仓库名。</param>
    public GitHubUpdateSource(HttpClient http, string owner = "SurinChi", string repository = "EasiLoginer")
    {
        ArgumentNullException.ThrowIfNull(http);

        _http = http;
        _owner = owner;
        _repository = repository;
    }

    /// <inheritdoc />
    public async Task<string?> FetchManifestJsonAsync(UpdateChannel channel, CancellationToken cancellationToken)
    {
        var url = channel == UpdateChannel.Dev
            ? $"{ApiRoot}/{_owner}/{_repository}/releases?per_page=1"
            : $"{ApiRoot}/{_owner}/{_repository}/releases/latest";

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.UserAgent.ParseAdd("EasiLoginer-UpdateChecker");
            request.Headers.Accept.ParseAdd("application/vnd.github+json");

            using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            return channel == UpdateChannel.Dev
                ? ConvertReleaseList(payload, channel)
                : ConvertSingleRelease(payload, channel);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            throw new IOException($"GitHub 更新源不可用：{exception.Message}", exception);
        }
    }

    private static string? ConvertReleaseList(string payload, UpdateChannel channel)
    {
        using var document = JsonDocument.Parse(payload);
        if (document.RootElement.ValueKind != JsonValueKind.Array || document.RootElement.GetArrayLength() == 0)
        {
            return null;
        }

        return BuildManifest(document.RootElement[0], channel);
    }

    private static string? ConvertSingleRelease(string payload, UpdateChannel channel)
    {
        using var document = JsonDocument.Parse(payload);
        return document.RootElement.ValueKind == JsonValueKind.Object
            ? BuildManifest(document.RootElement, channel)
            : null;
    }

    private static string? BuildManifest(JsonElement release, UpdateChannel channel)
    {
        var tag = release.TryGetProperty("tag_name", out var tagElement) ? tagElement.GetString() : null;
        if (string.IsNullOrWhiteSpace(tag))
        {
            return null;
        }

        // 标签可能带前缀 v（v0.0.2），PEP 440 解析器不认识
        var version = tag.TrimStart('v', 'V');

        var notes = release.TryGetProperty("body", out var bodyElement) && bodyElement.ValueKind == JsonValueKind.String
            ? bodyElement.GetString()
            : null;

        var assets = new JsonArray();
        if (release.TryGetProperty("assets", out var assetArray) && assetArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var asset in assetArray.EnumerateArray())
            {
                var url = asset.TryGetProperty("browser_download_url", out var urlElement)
                    ? urlElement.GetString()
                    : null;

                if (string.IsNullOrWhiteSpace(url))
                {
                    continue;
                }

                var name = asset.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null;
                var size = asset.TryGetProperty("size", out var sizeElement) && sizeElement.TryGetInt64(out var parsedSize)
                    ? parsedSize
                    : 0L;

                // GitHub 自 2025 起提供 digest："sha256:..."，没有就留空由安装前再校验
                var sha256 = asset.TryGetProperty("digest", out var digestElement) &&
                             digestElement.ValueKind == JsonValueKind.String
                    ? (digestElement.GetString() ?? string.Empty).Replace("sha256:", string.Empty, StringComparison.OrdinalIgnoreCase)
                    : string.Empty;

                assets.Add(new JsonObject
                {
                    ["source"] = "github",
                    ["url"] = url,
                    ["sha256"] = sha256,
                    ["size"] = size,
                    ["name"] = name ?? string.Empty,
                });
            }
        }

        var manifest = new JsonObject
        {
            ["version"] = version,
            ["channel"] = channel == UpdateChannel.Dev ? "dev" : "release",
            ["notes"] = notes ?? string.Empty,
            ["assets"] = assets,
        };

        return manifest.ToJsonString();
    }
}

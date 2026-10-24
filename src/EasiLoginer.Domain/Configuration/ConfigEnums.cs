using System.Text.Json.Serialization;

namespace EasiLoginer.Domain.Configuration;

/// <summary>登录方式，对应上游 <c>LoginMethod</c>；JSON 中写整数 0–3。</summary>
public enum LoginMethod
{
    /// <summary>固定位置（默认）。</summary>
    Fixed = 0,

    /// <summary>图像识别（上游已移除 OpenCV，仅保留命名与模板匹配）。</summary>
    Cv = 1,

    /// <summary>UI Automation 自动定位。</summary>
    Uia = 2,

    /// <summary>令牌投递（需要先修补）。</summary>
    Token = 3,
}

/// <summary>应用主题，对应上游 <c>ThemeOptions</c>；JSON 中写字符串。</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ThemeOptions>))]
public enum ThemeOptions
{
    /// <summary>跟随系统。</summary>
    Auto,

    /// <summary>浅色。</summary>
    Light,

    /// <summary>深色。</summary>
    Dark,
}

/// <summary>更新模式，对应上游 <c>UpdateMode</c>；JSON 中写整数 0–3。</summary>
public enum UpdateMode
{
    /// <summary>从不自动更新。EasiLoginer 首发默认值（文档 §5.1）。</summary>
    Never = 0,

    /// <summary>自动检查并通知。</summary>
    CheckAndNotify = 1,

    /// <summary>自动检查并下载。</summary>
    CheckAndDownload = 2,

    /// <summary>自动检查并安装。</summary>
    CheckAndInstall = 3,
}

/// <summary>更新通道，对应上游 <c>UpdateChannel</c>；JSON 中写字符串。</summary>
[JsonConverter(typeof(JsonStringEnumConverter<UpdateChannel>))]
public enum UpdateChannel
{
    /// <summary>稳定通道。</summary>
    [JsonStringEnumMemberName("release")]
    Release,

    /// <summary>测试通道。</summary>
    [JsonStringEnumMemberName("dev")]
    Dev,
}

/// <summary>下载镜像源，对应上游 <c>DownloadSource</c>；JSON 中写字符串。</summary>
[JsonConverter(typeof(JsonStringEnumConverter<DownloadSource>))]
public enum DownloadSource
{
    /// <summary>自动选择最快源。</summary>
    [JsonStringEnumMemberName("auto")]
    Auto,

    /// <summary>GitHub 直连。</summary>
    [JsonStringEnumMemberName("github")]
    GitHub,

    /// <summary>ghproxy 镜像。</summary>
    [JsonStringEnumMemberName("ghproxy")]
    GhProxy,

    /// <summary>ghfast 镜像。</summary>
    [JsonStringEnumMemberName("ghfast")]
    GhFast,
}

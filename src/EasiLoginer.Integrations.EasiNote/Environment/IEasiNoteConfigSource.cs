namespace EasiLoginer.Integrations.EasiNote.Environment;

/// <summary>希沃白板配置值来源（便于替换成真实 <c>Configs.fkv</c> 或测试桩）。</summary>
public interface IEasiNoteConfigSource
{
    /// <summary>按名称读取配置记录的值；不存在返回 <see langword="null"/>。</summary>
    string? ReadValue(string name);
}

/// <summary>从 <c>Configs.fkv</c> 文件读取配置。</summary>
public sealed class FkvConfigSource : IEasiNoteConfigSource
{
    private readonly string _path;

    /// <summary>以指定文件路径创建来源。</summary>
    public FkvConfigSource(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        _path = path;
    }

    /// <summary>使用 <c>%APPDATA%</c> 下的默认路径。</summary>
    public static FkvConfigSource FromEnvironment() => new(ConfigsFkv.DefaultPath);

    /// <inheritdoc />
    public string? ReadValue(string name) => ConfigsFkv.ReadValueFromFile(_path, name);
}

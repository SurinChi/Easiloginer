namespace EasiLoginer.Integrations.EasiNote.Path;

/// <summary>只读注册表访问端口（便于在无注册表写权限的环境下测试解析逻辑）。</summary>
public interface IRegistryReader
{
    /// <summary>读取 <c>HKEY_LOCAL_MACHINE</c> 下的字符串值；不存在或读取失败返回 <see langword="null"/>。</summary>
    string? ReadLocalMachineString(string subKey, string valueName);
}

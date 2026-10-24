using Microsoft.Extensions.Logging;
using EasiLoginer.Integrations.EasiNote.Pipe;

namespace EasiLoginer.Integrations.EasiNote.Patching;

/// <summary>希沃白板 DLL 修补端口。</summary>
public interface IEasiNotePatcher
{
    /// <summary>判断指定启动器对应的安装是否已修补。</summary>
    bool IsPatched(string easinoteExecutablePath);

    /// <summary>部署管道桥并修补 <c>EasiNote.Account.dll</c>。</summary>
    Task<bool> PatchAsync(string easinoteExecutablePath, CancellationToken cancellationToken = default);

    /// <summary>移除管道桥并恢复备份。</summary>
    Task<bool> UnpatchAsync(string easinoteExecutablePath, CancellationToken cancellationToken = default);

    /// <summary>查询白板当前登录态（需要已修补）。</summary>
    Task<CurrentLoginInfo?> ReadCurrentLoginAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// DLL 修补实现，对应上游 <c>integrations/easinote/patcher.py</c>。
/// </summary>
/// <remarks>
/// <para>目录约定：启动器位于 <c>&lt;版本&gt;\swenlauncher\swenlauncher.exe</c>，真实安装目录是它上面两级，
/// 目标为其下的 <c>EasiNote5_*/Main</c>。</para>
/// <para>
/// 修补顺序逐项照抄上游：<b>先</b>检查 <c>Newtonsoft.Json.dll</c> 是否被注入并从 <c>.bak</c> 恢复，
/// 再部署 <c>SeewoPipeBridge.dll</c>（内容相同跳过，不同则备份后覆盖），
/// 最后对每个 <c>EasiNote.Account.dll</c> 调用 <c>DllPatcher.exe</c>（30 秒超时、无窗口、退出码 0 为成功）。
/// </para>
/// </remarks>
public sealed class EasiNotePatcher : IEasiNotePatcher
{
    /// <summary>版本目录前缀。</summary>
    public const string VersionDirectoryPrefix = "EasiNote5_";

    /// <summary>管道桥文件名。</summary>
    public const string PipeBridgeFileName = "SeewoPipeBridge.dll";

    /// <summary>账号 DLL 文件名。</summary>
    public const string AccountDllFileName = "EasiNote.Account.dll";

    /// <summary>JSON 库文件名。</summary>
    public const string NewtonsoftFileName = "Newtonsoft.Json.dll";

    private static readonly byte[] PipeBridgeMarker = "SeewoPipeBridge"u8.ToArray();
    private static readonly byte[] StartBridgeMarker = "StartBridge"u8.ToArray();
    private static readonly TimeSpan PatcherTimeout = TimeSpan.FromSeconds(30);

    private readonly string _vendorsDirectory;
    private readonly IPatcherProcessRunner _runner;
    private readonly ILogger<EasiNotePatcher> _logger;
    private readonly SeewoPipeClient _pipeClient;

    /// <summary>创建补丁器。</summary>
    /// <param name="vendorsDirectory">随包的 <c>vendors/</c> 目录（含 <c>SeewoPipeBridge.dll</c> 与 <c>DLLPatcher/</c>）。</param>
    /// <param name="runner">外部补丁器执行器。</param>
    /// <param name="logger">日志。</param>
    /// <param name="pipeClient">管道客户端（可选，默认使用真实管道名）。</param>
    public EasiNotePatcher(
        string vendorsDirectory,
        IPatcherProcessRunner runner,
        ILogger<EasiNotePatcher> logger,
        SeewoPipeClient? pipeClient = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(vendorsDirectory);
        ArgumentNullException.ThrowIfNull(runner);
        ArgumentNullException.ThrowIfNull(logger);

        _vendorsDirectory = vendorsDirectory;
        _runner = runner;
        _logger = logger;
        _pipeClient = pipeClient ?? new SeewoPipeClient();
    }

    /// <inheritdoc />
    public bool IsPatched(string easinoteExecutablePath)
    {
        var targetDirectories = FindTargetDirectories(easinoteExecutablePath);
        if (targetDirectories.Count == 0)
        {
            return false;
        }

        foreach (var mainDirectory in targetDirectories)
        {
            var bridge = System.IO.Path.Combine(mainDirectory, PipeBridgeFileName);
            var account = System.IO.Path.Combine(mainDirectory, AccountDllFileName);

            if (!File.Exists(bridge) || !ContainsMarkers(account))
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc />
    public async Task<bool> PatchAsync(string easinoteExecutablePath, CancellationToken cancellationToken = default)
    {
        var targetDirectories = FindTargetDirectories(easinoteExecutablePath);
        if (targetDirectories.Count == 0)
        {
            _logger.LogWarning("无法在安装目录下找到 EasiNote5_*/Main");
            return false;
        }

        var bridgeSource = System.IO.Path.Combine(_vendorsDirectory, PipeBridgeFileName);
        var patcherExecutable = System.IO.Path.Combine(_vendorsDirectory, "DLLPatcher", "DllPatcher.exe");
        var allPatched = true;

        foreach (var mainDirectory in targetDirectories)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _logger.LogInformation("处理目录：{Directory}", mainDirectory);

            RestoreInjectedNewtonsoftIfNeeded(mainDirectory);

            if (!DeployFile(bridgeSource, System.IO.Path.Combine(mainDirectory, PipeBridgeFileName)))
            {
                allPatched = false;
            }

            if (!File.Exists(patcherExecutable))
            {
                _logger.LogWarning("DllPatcher 不可用：{Path}", patcherExecutable);
                allPatched = false;
                continue;
            }

            var accountDll = System.IO.Path.Combine(mainDirectory, AccountDllFileName);
            if (!File.Exists(accountDll))
            {
                _logger.LogDebug("跳过不存在的 {Path}", accountDll);
                continue;
            }

            var result = await _runner
                .RunAsync(patcherExecutable, accountDll, PatcherTimeout, cancellationToken)
                .ConfigureAwait(false);

            if (result.TimedOut)
            {
                _logger.LogError("DllPatcher 超时：{Path}", accountDll);
                allPatched = false;
                continue;
            }

            if (result.ExitCode == 0)
            {
                _logger.LogInformation("已修补：{Path}", accountDll);
            }
            else
            {
                _logger.LogWarning(
                    "修补失败：{Path}，退出码 {Code}，{Error}",
                    accountDll,
                    result.ExitCode,
                    string.IsNullOrWhiteSpace(result.StandardError) ? "(无错误输出)" : result.StandardError);
                allPatched = false;
            }
        }

        return allPatched;
    }

    /// <inheritdoc />
    public async Task<bool> UnpatchAsync(string easinoteExecutablePath, CancellationToken cancellationToken = default)
    {
        var targetDirectories = FindTargetDirectories(easinoteExecutablePath);
        if (targetDirectories.Count == 0)
        {
            _logger.LogWarning("无法在安装目录下找到 EasiNote5_*/Main");
            return false;
        }

        var allUnpatched = true;

        foreach (var mainDirectory in targetDirectories)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var bridge = System.IO.Path.Combine(mainDirectory, PipeBridgeFileName);
            if (File.Exists(bridge))
            {
                try
                {
                    File.Delete(bridge);
                    _logger.LogInformation("已移除：{Path}", bridge);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    _logger.LogError(exception, "移除失败：{Path}", bridge);
                    allUnpatched = false;
                    continue;
                }
            }

            var newtonsoft = System.IO.Path.Combine(mainDirectory, NewtonsoftFileName);
            if (File.Exists(newtonsoft) && ContainsMarker(newtonsoft, PipeBridgeMarker) && !RestoreFromBackup(newtonsoft))
            {
                _logger.LogWarning("{Name} 已被修改但无可用备份", NewtonsoftFileName);
                allUnpatched = false;
            }

            var account = System.IO.Path.Combine(mainDirectory, AccountDllFileName);
            if (File.Exists(account) && File.Exists(account + ".bak") && !RestoreFromBackup(account))
            {
                _logger.LogWarning("恢复 {Name} 失败", AccountDllFileName);
                allUnpatched = false;
            }
        }

        await Task.CompletedTask.ConfigureAwait(false);
        return allUnpatched;
    }

    /// <inheritdoc />
    public Task<CurrentLoginInfo?> ReadCurrentLoginAsync(CancellationToken cancellationToken = default)
        => _pipeClient.ReadCurrentLoginInfoAsync(addToLoggedTokens: false, cancellationToken);

    private void RestoreInjectedNewtonsoftIfNeeded(string mainDirectory)
    {
        var newtonsoft = System.IO.Path.Combine(mainDirectory, NewtonsoftFileName);
        var backup = newtonsoft + ".bak";

        if (!File.Exists(newtonsoft) || !File.Exists(backup))
        {
            return;
        }

        if (!ContainsMarker(newtonsoft, PipeBridgeMarker))
        {
            return;
        }

        _logger.LogWarning("检测到 {Name} 已被注入，从备份恢复", NewtonsoftFileName);
        try
        {
            File.Copy(backup, newtonsoft, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(exception, "恢复 {Path} 失败", newtonsoft);
        }
    }

    /// <summary>部署文件：内容相同跳过，不同则备份后覆盖。</summary>
    private bool DeployFile(string source, string destination)
    {
        if (!File.Exists(source))
        {
            _logger.LogWarning("源文件不存在：{Path}", source);
            return false;
        }

        if (File.Exists(destination))
        {
            try
            {
                if (File.ReadAllBytes(source).AsSpan().SequenceEqual(File.ReadAllBytes(destination)))
                {
                    _logger.LogDebug("文件内容一致，跳过：{Name}", System.IO.Path.GetFileName(destination));
                    return true;
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                _logger.LogError(exception, "读取文件失败");
                return false;
            }

            var backup = destination + ".bak";
            try
            {
                if (File.Exists(backup))
                {
                    File.Delete(backup);
                }

                File.Move(destination, backup);
                _logger.LogInformation("已备份：{Path}", backup);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                _logger.LogError(exception, "备份失败：{Path}", destination);
                return false;
            }
        }

        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(destination)!);
            File.Copy(source, destination, overwrite: true);
            _logger.LogInformation("已部署：{Path}", destination);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(exception, "写入失败：{Path}", destination);
            return false;
        }
    }

    private static bool RestoreFromBackup(string filePath)
    {
        var backup = filePath + ".bak";
        if (!File.Exists(backup))
        {
            return false;
        }

        try
        {
            File.Copy(backup, filePath, overwrite: true);
            File.Delete(backup);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool ContainsMarkers(string filePath)
        => ContainsMarker(filePath, PipeBridgeMarker) && ContainsMarker(filePath, StartBridgeMarker);

    private static bool ContainsMarker(string filePath, byte[] marker)
    {
        try
        {
            return File.ReadAllBytes(filePath).AsSpan().IndexOf(marker) >= 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>从启动器路径推导真实安装目录并列出 <c>EasiNote5_*/Main</c>。</summary>
    private static List<string> FindTargetDirectories(string easinoteExecutablePath)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(easinoteExecutablePath))
        {
            return result;
        }

        var launcherDirectory = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(easinoteExecutablePath));
        var baseDirectory = launcherDirectory is null ? null : System.IO.Path.GetDirectoryName(launcherDirectory);

        if (baseDirectory is null || !Directory.Exists(baseDirectory))
        {
            return result;
        }

        foreach (var child in Directory.EnumerateDirectories(baseDirectory))
        {
            var name = System.IO.Path.GetFileName(child);
            if (!name.StartsWith(VersionDirectoryPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            var mainDirectory = System.IO.Path.Combine(child, "Main");
            if (Directory.Exists(mainDirectory))
            {
                result.Add(mainDirectory);
            }
        }

        return result;
    }
}

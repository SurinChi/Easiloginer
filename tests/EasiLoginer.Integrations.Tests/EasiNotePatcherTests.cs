using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using EasiLoginer.Integrations.EasiNote.Patching;
using FluentAssertions;

namespace EasiLoginer.Integrations.Tests;

/// <summary>
/// DLL 补丁器（文档 §8.5）。
/// </summary>
/// <remarks>
/// 全部在临时目录里模拟安装结构，使用假 runner 代替真实的 <c>DllPatcher.exe</c>，
/// <b>不会读写本机真实的希沃安装</b>。
/// </remarks>
public sealed class EasiNotePatcherTests : IDisposable
{
    private const string BridgeContent = "BRIDGE";
    private const string PatchedAccountContent = "ACCOUNT SeewoPipeBridge StartBridge";

    private readonly string _root;
    private readonly string _vendors;
    private readonly string _install;
    private readonly string _mainDirectory;
    private readonly string _executable;

    public EasiNotePatcherTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "EasiLoginerTests", "patcher", Guid.NewGuid().ToString("N"));
        _vendors = Path.Combine(_root, "vendors");
        _install = Path.Combine(_root, "install");
        _mainDirectory = Path.Combine(_install, "EasiNote5_5.2.4", "Main");
        _executable = Path.Combine(_install, "swenlauncher", "swenlauncher.exe");

        Directory.CreateDirectory(_mainDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(_executable)!);
        Directory.CreateDirectory(Path.Combine(_vendors, "DLLPatcher"));

        File.WriteAllText(_executable, "launcher");
        File.WriteAllText(Path.Combine(_mainDirectory, "EasiNote.Account.dll"), "ACCOUNT");
        File.WriteAllText(Path.Combine(_mainDirectory, "Newtonsoft.Json.dll"), "JSON");
        File.WriteAllText(Path.Combine(_vendors, "SeewoPipeBridge.dll"), BridgeContent);
        File.WriteAllText(Path.Combine(_vendors, "DLLPatcher", "DllPatcher.exe"), "patcher");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    /// <summary>未部署管道桥时视为未修补。</summary>
    [Fact]
    public void 初始状态未修补()
    {
        CreatePatcher(new FakeRunner(_ => Success())).IsPatched(_executable).Should().BeFalse();
    }

    /// <summary>修补成功后管道桥就位、账号 DLL 含标记。</summary>
    [Fact]
    public async Task 修补成功()
    {
        var patcher = CreatePatcher(new FakeRunner(path =>
        {
            File.WriteAllText(path, PatchedAccountContent);
            return Success();
        }));

        var patched = await patcher.PatchAsync(_executable);

        patched.Should().BeTrue();
        File.ReadAllText(Path.Combine(_mainDirectory, "SeewoPipeBridge.dll")).Should().Be(BridgeContent);
        patcher.IsPatched(_executable).Should().BeTrue();
    }

    /// <summary>已有不同内容时先备份再覆盖。</summary>
    [Fact]
    public async Task 覆盖前创建备份()
    {
        var bridgePath = Path.Combine(_mainDirectory, "SeewoPipeBridge.dll");
        File.WriteAllText(bridgePath, "OLD-BRIDGE");

        var patcher = CreatePatcher(new FakeRunner(path =>
        {
            File.WriteAllText(path, PatchedAccountContent);
            return Success();
        }));

        await patcher.PatchAsync(_executable);

        File.ReadAllText(bridgePath).Should().Be(BridgeContent);
        File.ReadAllText(bridgePath + ".bak").Should().Be("OLD-BRIDGE");
    }

    /// <summary>内容一致时不产生备份文件。</summary>
    [Fact]
    public async Task 内容一致不产生备份()
    {
        var bridgePath = Path.Combine(_mainDirectory, "SeewoPipeBridge.dll");
        File.WriteAllText(bridgePath, BridgeContent);

        var patcher = CreatePatcher(new FakeRunner(path =>
        {
            File.WriteAllText(path, PatchedAccountContent);
            return Success();
        }));

        await patcher.PatchAsync(_executable);

        File.Exists(bridgePath + ".bak").Should().BeFalse();
    }

    /// <summary>缺 DllPatcher.exe 时明确失败，而不是假装成功。</summary>
    [Fact]
    public async Task 缺少补丁器时失败()
    {
        File.Delete(Path.Combine(_vendors, "DLLPatcher", "DllPatcher.exe"));

        var patched = await CreatePatcher(new FakeRunner(_ => Success())).PatchAsync(_executable);

        patched.Should().BeFalse();
    }

    /// <summary>补丁器非 0 退出时失败。</summary>
    [Fact]
    public async Task 补丁器失败时返回失败()
    {
        var patched = await CreatePatcher(new FakeRunner(_ => new PatcherRunResult(1, "boom", false)))
            .PatchAsync(_executable);

        patched.Should().BeFalse();
    }

    /// <summary>补丁器超时同样算失败。</summary>
    [Fact]
    public async Task 补丁器超时返回失败()
    {
        var patched = await CreatePatcher(new FakeRunner(_ => new PatcherRunResult(-1, string.Empty, TimedOut: true)))
            .PatchAsync(_executable);

        patched.Should().BeFalse();
    }

    /// <summary>找不到版本目录时失败且不抛异常。</summary>
    [Fact]
    public async Task 找不到版本目录时失败()
    {
        var otherExe = Path.Combine(_root, "empty", "swenlauncher", "swenlauncher.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(otherExe)!);
        File.WriteAllText(otherExe, "x");

        var patcher = CreatePatcher(new FakeRunner(_ => Success()));

        patcher.IsPatched(otherExe).Should().BeFalse();
        (await patcher.PatchAsync(otherExe)).Should().BeFalse();
    }

    /// <summary>已经被注入过的 Newtonsoft.Json.dll 必须在修补前从备份恢复。</summary>
    [Fact]
    public async Task 注入过的JSON库先恢复()
    {
        var jsonPath = Path.Combine(_mainDirectory, "Newtonsoft.Json.dll");
        File.WriteAllText(jsonPath, "JSON SeewoPipeBridge injected");
        File.WriteAllText(jsonPath + ".bak", "JSON");

        await CreatePatcher(new FakeRunner(path =>
        {
            File.WriteAllText(path, PatchedAccountContent);
            return Success();
        })).PatchAsync(_executable);

        File.ReadAllText(jsonPath).Should().Be("JSON");
    }

    /// <summary>撤销：移除管道桥并恢复账号 DLL。</summary>
    [Fact]
    public async Task 撤销修补()
    {
        var bridgePath = Path.Combine(_mainDirectory, "SeewoPipeBridge.dll");
        var accountPath = Path.Combine(_mainDirectory, "EasiNote.Account.dll");

        var patcher = CreatePatcher(new FakeRunner(path =>
        {
            File.WriteAllText(path, PatchedAccountContent);
            return Success();
        }));

        await patcher.PatchAsync(_executable);
        File.WriteAllText(accountPath + ".bak", "ACCOUNT");

        (await patcher.UnpatchAsync(_executable)).Should().BeTrue();

        File.Exists(bridgePath).Should().BeFalse();
        File.ReadAllText(accountPath).Should().Be("ACCOUNT", "应从 .bak 恢复原始 DLL");
        File.Exists(accountPath + ".bak").Should().BeFalse();
        patcher.IsPatched(_executable).Should().BeFalse();
    }

    /// <summary>撤销时可安全处理不存在的管道桥。</summary>
    [Fact]
    public async Task 撤销幂等()
    {
        var patcher = CreatePatcher(new FakeRunner(_ => Success()));

        (await patcher.UnpatchAsync(_executable)).Should().BeTrue();
        (await patcher.UnpatchAsync(_executable)).Should().BeTrue();
    }

    private EasiNotePatcher CreatePatcher(IPatcherProcessRunner runner)
        => new(_vendors, runner, NullLogger<EasiNotePatcher>.Instance);

    private static PatcherRunResult Success() => new(0, string.Empty, false);

    private sealed class FakeRunner(Func<string, PatcherRunResult> behavior) : IPatcherProcessRunner
    {
        public List<string> Calls { get; } = [];

        public Task<PatcherRunResult> RunAsync(string executablePath, string argument, TimeSpan timeout, CancellationToken cancellationToken)
        {
            Calls.Add(argument);
            return Task.FromResult(behavior(argument));
        }
    }
}

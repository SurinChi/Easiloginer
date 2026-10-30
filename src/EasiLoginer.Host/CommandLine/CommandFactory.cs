using System.CommandLine;

namespace EasiLoginer.Host.CommandLine;

/// <summary>
/// 命令行定义，对应文档 §6.3 的命令矩阵。
/// </summary>
/// <remarks>
/// <c>patch</c> 刻意排在所有命令之外：它绕过单实例、可提权运行，因此分发前
/// 必须能被单独识别（见 <see cref="AppBootstrap"/>）。
/// </remarks>
internal static class CommandFactory
{
    /// <summary>构造根命令与全部子命令。</summary>
    public static RootCommand Create()
    {
        var root = new RootCommand("EasiLoginer —— 手动触发希沃白板自动登录");
        root.SetAction((_, _) => Task.FromResult(CommandHandlers.Settings()));

        root.Add(CreateSettingsCommand());
        root.Add(CreateLoginCommand());
        root.Add(CreateOobeCommand());
        root.Add(CreateSkipCommand());
        root.Add(CreatePatchCommand());
        return root;
    }

    private static Command CreateSettingsCommand()
    {
        var command = new Command("settings", "打开主界面");
        command.SetAction((_, _) => Task.FromResult(CommandHandlers.Settings()));
        return command;
    }

    private static Command CreateLoginCommand()
    {
        var profileId = new Option<string>("--id", "-i") { Description = "档案 ID" };
        var account = new Option<string>("--account", "-a") { Description = "希沃账号" };
        var password = new Option<string>("--password", "-p") { Description = "希沃密码" };
        var manual = new Option<bool>("--manual", "-m") { Description = "跳过运行前确认弹窗" };

        var command = new Command("login", "手动执行一次登录（仅由用户或快捷方式发起）");
        command.Add(profileId);
        command.Add(account);
        command.Add(password);
        command.Add(manual);

        command.SetAction((parseResult, cancellationToken) => CommandHandlers.LoginAsync(
            parseResult.GetValue(profileId),
            parseResult.GetValue(account),
            parseResult.GetValue(password),
            parseResult.GetValue(manual),
            cancellationToken));

        return command;
    }

    private static Command CreateOobeCommand()
    {
        var command = new Command("oobe", "重新进入首次设置向导");
        command.SetAction((_, _) => Task.FromResult(CommandHandlers.Oobe()));
        return command;
    }

    private static Command CreateSkipCommand()
    {
        var command = new Command("skip", "跳过一次自动登录（写 SkipOnce 后退出）");
        command.SetAction((_, _) => Task.FromResult(CommandHandlers.Skip()));
        return command;
    }

    private static Command CreatePatchCommand()
    {
        var on = new Option<bool>("--on") { Description = "修补希沃白板" };
        var off = new Option<bool>("--off") { Description = "撤销修补" };

        var command = new Command("patch", "修补或撤销希沃白板的登录桥");
        command.Add(on);
        command.Add(off);

        command.SetAction((parseResult, _) => Task.FromResult(CommandHandlers.Patch(
            parseResult.GetValue(on),
            parseResult.GetValue(off))));

        return command;
    }
}

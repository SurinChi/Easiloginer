# 第三方依赖声明

EasiLoginer 以 GNU GPLv3 发布。下表列出**实际被引用的**第三方包及其许可，
版本以 `Directory.Packages.props`（中央版本管理）为准。

> 声明基于各包公开的许可信息整理；发布前请以包内 `LICENSE` 文件与
> `dotnet list package --include-transitive` 的输出再核对一次。

## 运行时依赖

| 包 | 版本 | 许可 | 用途 |
|---|---|---|---|
| WPF-UI | 4.3.0 | MIT | Fluent 窗口与控件 |
| CommunityToolkit.Mvvm | 8.4.2 | MIT | 视图模型（源生成器） |
| Serilog | 4.4.0 | Apache-2.0 | 结构化日志 |
| Serilog.Sinks.Console | 6.1.1 | Apache-2.0 | 控制台日志 |
| Serilog.Sinks.File | 7.0.0 | Apache-2.0 | 文件日志与脱敏滚动 |
| Serilog.Sinks.Async | 2.1.0 | Apache-2.0 | 异步写日志，避免阻塞 UI |
| Sentry | 6.12.1 | MIT | 崩溃与错误上报（默认关闭，需用户开启并配置 DSN） |
| Sentry.Serilog | 6.12.1 | MIT | 把 Serilog 事件送入 Sentry |
| System.CommandLine | 2.0.13 | MIT | 命令行解析 |
| Microsoft.Extensions.Hosting | 10.0.13 | MIT | 宿主与依赖注入 |
| Microsoft.Extensions.Logging.Abstractions | 10.0.13 | MIT | 日志抽象 |
| Konscious.Security.Cryptography.Argon2 | 1.3.1 | MIT | Argon2id 主密钥派生 |
| System.Security.Cryptography.ProtectedData | 10.0.13 | MIT | DPAPI 缓存主密钥 |
| dnlib | 4.5.0 | MIT | DllPatcher 读写希沃程序集 |

## 测试依赖（不随发布分发）

| 包 | 版本 | 许可 | 用途 |
|---|---|---|---|
| xunit | 2.9.3 | Apache-2.0 | 单元测试框架 |
| xunit.runner.visualstudio | 3.1.4 | Apache-2.0 | 测试运行器 |
| Microsoft.NET.Test.Sdk | 17.14.1 | MIT | 测试宿主 |
| FluentAssertions | 7.2.2 | Apache-2.0 | 断言（**锁定 7.x**：8.x 起改为 Xceed 商业许可） |
| coverlet.collector | 6.0.4 | MIT | 覆盖率采集 |

## 平台组件（随系统或运行时分发）

| 组件 | 来源 | 说明 |
|---|---|---|
| .NET 10 运行时 | Microsoft | 自包含发布时随包附带，MIT |
| UIAutomationClient / UIAutomationTypes | Windows | UI Automation 客户端，系统组件 |
| SeewoPipeBridge.dll | 上游 EasiAuto（GPLv3） | 修补希沃时部署的管道桥；上游以 Git LFS 存放，需放置实体文件到 `vendors/` |

## 图标与素材

| 素材 | 来源 | 许可 |
|---|---|---|
| 导航图标 `gear-24` / `person-24` / `download-24` / `info-24` | [GitHub Octicons](https://github.com/primer/octicons)（浏览站 <https://octicons.andre601.ch/>） | MIT，Copyright (c) GitHub Inc. |
| `EasiLoginer` 应用图标（EL 绿底） | SurinChi 自有 | 随本项目 GPLv3 |

Octicons 的 SVG 路径以 `GeometryDrawing` 形式内联在 `src/EasiLoginer.Presentation/Shell/MainWindow.xaml`，
未引入额外字体或第三方 DLL；替换图标只需改这四条 `DrawingImage`。

## 上游项目

本项目是 [EasiAuto](https://github.com/hxabcd/EasiAuto)（作者 hxabcd）的 C# 重写，
遵循 GPLv3 保留原作者署名与许可声明，详见 `NOTICE`。希沃白板本身是希沃公司的商业软件，
本项目不包含其任何二进制文件。

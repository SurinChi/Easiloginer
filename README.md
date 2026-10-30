# EasiLoginer

EasiLoginer 是 EasiAuto 的 C# 重构项目，用于手动触发希沃白板自动登录。

- **作者 / 维护者**：SurinChi
- **上游项目**：[EasiAuto](https://github.com/hxabcd/EasiAuto)，作者 hxabcd
- **许可证**：GNU General Public License v3.0，见 [LICENSE](LICENSE) 与 [NOTICE](NOTICE)
- **当前状态**：**M1–M7 完成（当前版本 0.0.3）**。
  - 登录：固定位置（10.4s）、**UI Automation（5.8–6.9s 一次成功）**、**图像识别**（逐像素模板匹配，仅支持 1920×1080 100% 与 4K 200%）、令牌投递（管道桥 DLL 已随包提供）
  - 更新：读取本仓库的 GitHub Releases（`v0.0.1` 已验证端到端）
  - 数据保留：超过 N 天未启动自动清空数据并恢复初始状态（默认 90 天，最短 7 天，可在配置页关闭）
  - CI：`.github/workflows/ci.yml`（windows-latest + .NET 10，构建 + 全部测试）
  - **351 个测试全绿**（6 个测试项目）
  - 明确不做：公告（无端点，也不再规划）；仍未做：主题令牌表、代码签名

> **Smart App Control 与程序集命名**：本机 SAC 对**程序集身份** `EasiLoginer.Automation` 有拦截规则——
> 无论内容/版本/路径如何，加载它都会返回 `0x800711C7`（An Application Control policy has blocked this file）。
> 同源码换个程序集名即可正常加载，因此自动化程序集改名为 **`EasiLoginer.AutomationKit`**
> （项目名、命名空间、目录结构均不变；见 `src/EasiLoginer.Automation/EasiLoginer.Automation.csproj` 注释）。
> 正式分发仍应做代码签名（文档 §12.5），否则 SAC 机器上的用户可能遇到同类拦截。

## 与上游的关系

EasiLoginer 基于 EasiAuto 的产品行为与数据格式进行 C# 重写，继续以 GPLv3 开源。
上游的版权与来源声明保留在 `NOTICE` 中；本项目新增 `Copyright (C) 2026 SurinChi`。

本项目**不支持 ClassIsland**，登录仅由用户在应用内、命令行或快捷方式手动触发。

## 设计文档

- [EasiLoginer C# 重构框架](EasiLoginer-CSharp-重构框架.md)

## 开发

### 环境

- **.NET 10 SDK**（本仓库 `global.json` 锁定 `10.0.402`，`rollForward: latestFeature`）
- 若 SDK 安装在用户目录（如 `%USERPROFILE%\.dotnet`），构建前需把该目录加入 `PATH`：

```powershell
$env:PATH = "$env:USERPROFILE\.dotnet;$env:PATH"
```

### 构建与测试

```powershell
dotnet build EasiLoginer.sln -c Release
dotnet test  EasiLoginer.sln -c Release --no-build
```

零警告是硬要求（`Directory.Build.props` 已开 `TreatWarningsAsErrors`）。

### 分层强制

`tests/EasiLoginer.Architecture.Tests` 以**只读 PE 元数据**的方式检查依赖方向，
不加载被测程序集。原因：开启 Smart App Control 的机器上，`Assembly.Load`
会按哈希口碑拦截新编译的无签名程序集（`0x800711C7`），导致测试随机失败；
只读元数据不执行代码，结果确定且更快。

规则与文档 §4.3 一致：Domain 仅依赖 BCL、Application 只依赖 Domain、
实现层互不引用、Presentation 不直接引用实现层、任何层都不反向引用 Host。


## 命令行

登录**只由用户手动触发**：应用内「运行」、命令行、快捷方式。不存在定时、计划任务、
文件/进程监视等自动触发路径，也不读取 ClassIsland 配置。

| 命令 | 说明 |
|---|---|
| 无参数 / `settings` | 打开主界面 |
| `login -i <档案ID>` | 用档案登录 |
| `login -a <账号> -p <密码>` | 用账号密码登录（`-i` 与 `-a` 互斥） |
| `login ... -m` | 跳过运行前确认弹窗 |
| `skip` | 写一次 SkipOnce 后退出 |
| `oobe` | 重新进入首次设置向导 |
| `patch --on` / `patch --off` | 修补 / 撤销希沃登录桥（绕过单实例） |

退出码：`0` 成功、`1` 失败或参数非法、`20–29` 为 `patch` 专用码
（20 成功、21 操作失败、22 未找到希沃、29 未知异常）。

> **版本**：首发 `0.0.1`，当前 `0.0.3`（由 SurinChi 决定，不使用文档示例中的 2.0.0）。

## 发布

```cmd
build\publish.cmd
```

产物：`..\EasiLoginer-dist\EasiLoginer_0.0.3.zip`（约 107 MB，自包含 win-x64，无需预装 .NET）。**输出在仓库之外**，避免构建物污染工作区。

ZIP 顶层为 `EasiLoginer/`，内容：

| 路径 | 说明 |
|---|---|
| `EasiLoginer.exe` + 运行时 | 自包含发布，双击即用 |
| `LICENSE` / `NOTICE` / `THIRD-PARTY-NOTICES.md` / `README.md` | 开源声明随包分发（GPLv3 要求） |
| `data/` | 运行时生成 `config.json`、`profile.json`、日志与缓存 |
| `resources/` | 图标与希沃界面模板（当前为空占位） |
| `vendors/DLLPatcher/DllPatcher.exe` | 由 `src/EasiLoginer.Patcher` 发布 |

发布参数与文档 §12.1 一致：`-c Release -r win-x64 --self-contained true -p:PublishReadyToRun=true -p:DebugType=embedded`，`PublishSingleFile=false`、`PublishTrimmed=false`。

> `vendors\SeewoPipeBridge.dll`（22 016 字节，SHA256 `73e31471…d35b4`）已随仓库提供，发布脚本会自动复制进 ZIP 的 `vendors/`。详见 [vendors/README.md](vendors/README.md)。

ZIP 由 `build/make-zip.ps1` 生成：条目一律使用正斜杠、保留顶层目录、包含空目录，符合 ZIP 规范。

## 界面资源

| 资源 | 位置 | 说明 |
|---|---|---|
| 应用图标（PNG） | [src/EasiLoginer.Presentation/Assets/easiloginer.png](src/EasiLoginer.Presentation/Assets/easiloginer.png) | 窗口图标与关于页图标（pack URI 引用），也是设计源图 |
| 应用图标（ICO） | [src/EasiLoginer.Host/Assets/easiloginer.ico](src/EasiLoginer.Host/Assets/easiloginer.ico) | exe 图标（`<ApplicationIcon>`），含 16/24/32/48/64/128/256 七种尺寸（由上面的 PNG 生成） |
| 导航图标 | `MainWindow.xaml` 内的 `DrawingImage` | GitHub Octicons（MIT）：`gear-24`、`person-24`、`download-24`、`info-24`，以矢量路径内联，无需字体或第三方 DLL |

替换应用图标：同时替换 `Assets/easiloginer.png`，并据其重新生成 `Assets/easiloginer.ico`（多尺寸 ICO），两处保持一致。

导航图标的着色使用 `{DynamicResource TextFillColorPrimaryBrush}`，随明暗主题自动适配；尺寸固定 16 DIP，与 WPF-UI 导航默认一致。署名见 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)。

## 数据保留与清理

应用**只写本地文件**，不做任何遥测上报（Sentry 已移除，`App.TelemetryEnabled` 仅为与上游配置兼容而保留）。

超过配置天数未启动时，下次启动会**清空 `data/` 下的全部数据**（配置、档案、日志、缓存），恢复首次安装状态：

- 配置项：`App.ResetDataAfterDays`（配置页 → 应用设置 →「未启动清理」）；
- 默认 **90 天**（约 3 个月）；
- 填 `0` 表示关闭；
- 启用时**最短 7 天**，填更小的值会按 7 天处理（防止"启动即清空"）；
- 判断依据是 `data/state.json` 里记录的上次启动时间，与实际运行时长无关。

## 第三方依赖

发布前需根据锁定版本生成 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)。

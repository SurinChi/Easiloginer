# 构建与发布流程

本文是 EasiLoginer 的标准发版流程。**版本号只有一个来源**：根目录
`Directory.Build.props` 的 `<Version>`；`build\publish.cmd` 会自动读取它并传给
dotnet、Inno Setup 与发布材料生成器。

## 1. 发布前检查

| 检查项 | 要求 |
|---|---|
| 分支状态 | `main` 与 `origin/main` 一致，工作区干净（`git status` 无改动） |
| CI | 最近一次 `ci`（build + test）为绿色 |
| 测试 | 本地 `dotnet test EasiLoginer.sln -c Release` 全绿 |
| 版本号 | 只需改 `Directory.Build.props` 的 4 个版本字段（见下） |
| README | 测试数量、功能说明、截图与当前版本一致 |
| 工具 | .NET 10 SDK、7-Zip、Inno Setup 6（安装包需要后两者） |

### 版本命名约定

| 用途 | 格式 | 示例 |
|---|---|---|
| 稳定版 | `X.Y.Z` | `0.2.0` |
| Alpha | `X.Y.Z-alpha.N` | `0.2.0-alpha.1`、`0.2.0-alpha.2` |
| Beta | `X.Y.Z-beta.N` | `0.2.0-beta.1` |
| Git 标签 | `v` + 完整版本号 | `v0.2.0-alpha.1` |

规则：

- **测试版不得占用将来的稳定版号**。`0.2.0-alpha.1` 之后，稳定版仍然是 `0.2.0`；
  `v0.2.0` 这个标签只在 Alpha/Beta 全部结束、准备正式发布时才创建。
- 测试序号 `N` 从 1 递增；`alpha.1 → alpha.2 → beta.1 → 0.2.0` 的排序由
  `Pep440Version` 正确处理（alpha < beta < 正式版）。
- 程序集与安装包的数字版本始终是 `X.Y.Z.0`（去掉 `-alpha.N` / `-beta.N`）；
  `AssemblyVersion`、`FileVersion`、Inno 的 `VersionInfoVersion` 都用这个值。
- 可执行文件的 `ProductVersion` / `InformationalVersion` 保留完整版本号，更新检查与
  关于页显示的就是它。

版本号要改的 4 处（同一个文件里连续 4 行）：

```xml
<Version>0.2.0-alpha.1</Version>
<AssemblyVersion>0.2.0.0</AssemblyVersion>
<FileVersion>0.2.0.0</FileVersion>
<InformationalVersion>0.2.0-alpha.1</InformationalVersion>
```

`build\get-version.ps1` 负责校验版本格式，并提供数字版本：

```powershell
build\get-version.ps1                    # 0.2.0-alpha.1
build\get-version.ps1 -Format Numeric    # 0.2.0.0
```

`build\installer.iss` 里的 `MyAppVersion` 只是**单独编译安装脚本时的兜底默认值**，
正常走 `publish.cmd` 时会被命令行 `/DMyAppVersion=...` 覆盖，不必手改；
但建议发版时顺手同步，避免两者漂移。

## 2. 构建

```cmd
build\publish.cmd
```

脚本固定做 8 步：

1. `dotnet publish` 主程序（self-contained win-x64、ReadyToRun）
2. 组装绿色版目录（复制 LICENSE / NOTICE / THIRD-PARTY-NOTICES / README）
3. 发布 `DllPatcher` 到 `vendors\DLLPatcher`
4. 复制 `SeewoPipeBridge.dll`
5. 生成绿色版 ZIP（正斜杠、保留顶层目录与空目录）
6. 生成 LZMA2 负载 `payload.7z`，并附带 7-Zip 工具
7. 用 Inno Setup 生成安装包
8. **生成发布材料**：`RELEASE-<版本>.md` 发布说明 + `SHA256SUMS-<版本>.txt`

### 发布说明从哪来

`make-release.ps1` 按以下顺序决定 `RELEASE-<版本>.md` 的正文：

1. 若存在 `docs\releases\<版本>.md`（人工润色过的发布说明）→ 以它为正文，自动追加
   构建日期、构建提交与 SHA-256 校验表；
2. 否则 → 用自上一个 `v*` 标签以来的提交标题生成草稿，发布前再润色。

所以标准做法是：发版前先写 `docs\releases\<版本>.md`（参考
[`docs/releases/0.2.0-alpha.1.md`](../docs/releases/0.2.0-alpha.1.md)），再跑 `publish.cmd`，
`RELEASE-<版本>.md` 就是可以直接粘贴到 GitHub Release 的完整正文。

测试版也建议单独写一份说明（例如 `0.2.0-alpha.1.md` 和 `0.2.0-beta.1.md`），
不要复用稳定版的正文，避免版本号、下载文件名和升级说明对不上。

产物全部在仓库外，**不污染工作区**：

```
..\EasiLoginer-dist\
├─ EasiLoginer_<版本>.zip          绿色版
├─ EasiLoginer-Setup-<版本>.exe    安装包
├─ RELEASE-<版本>.md               发布说明（带校验和，直接粘到 GitHub Release）
├─ SHA256SUMS-<版本>.txt           校验和清单
├─ out\                            主程序发布输出
├─ stage\EasiLoginer\              打包暂存目录
├─ payload.7z / tools\             安装包内部负载与 7-Zip
└─ （历史版本产物会保留，按需清理）
```

构建失败时看控制台最后一段即可：缺 7-Zip / Inno Setup 时脚本会明确提示，
此时绿色版 ZIP 通常已经生成。

## 3. 验证产物

```powershell
# 版本号
(Get-Item ..\EasiLoginer-dist\out\EasiLoginer.exe).VersionInfo |
    Select-Object FileVersion, ProductVersion

# 安装包版本：FileVersion 是数字版本，ProductVersion 是完整版本
(Get-Item ..\EasiLoginer-dist\EasiLoginer-Setup-0.2.0-alpha.1.exe).VersionInfo |
    Select-Object FileVersion, ProductVersion

# 校验和（与 SHA256SUMS-<版本>.txt 对照）
Get-FileHash ..\EasiLoginer-dist\EasiLoginer_0.2.0-alpha.1.zip -Algorithm SHA256
```

建议再手动跑一次绿色版：解压 → `EasiLoginer\EasiLoginer.exe`，确认能打开、
「设置 / 更新 / 关于」正常。

## 4. 发布到 GitHub Release

仓库的 `main` 开了分支保护（必须走 PR），**标签不受影响**：

```powershell
# 1) 确认本地 main 与远端一致（测试版可在 alpha/beta 分支打标签）
git fetch origin
git reset --hard origin/main

# 2) 打标签并推送（测试版示例；稳定版换成 v0.2.0）
git tag -a v0.2.0-alpha.1 -m "EasiLoginer 0.2.0 Alpha 1"
git push origin v0.2.0-alpha.1
```

然后在网页：

1. `https://github.com/SurinChi/Easiloginer/releases/new`
2. 选择刚推送的 `v0.2.0-alpha.1` 标签，标题填 `EasiLoginer 0.2.0 Alpha 1`
3. 正文直接粘贴 `RELEASE-0.2.0-alpha.1.md`（已包含构建信息与校验和；如需补充面向用户的说明，先改 `docs\releases\0.2.0-alpha.1.md` 再重新生成）
4. 上传附件：`EasiLoginer-Setup-0.2.0-alpha.1.exe`、`EasiLoginer_0.2.0-alpha.1.zip`，以及 `SHA256SUMS-0.2.0-alpha.1.txt`
5. **测试版必须勾选 `Set as a pre-release`，不要勾 `Set as the latest release`**；稳定版才勾 latest

发布前确认附件不是空的：更新页依赖 Release 的 `assets[]`，没有附件的 Release
只会提示「该版本没有可下载的安装包」。

## 5. 发布公告（可选但推荐）

公告与版本解耦，改完提交即生效，不需要重新发版：

1. 编辑 `announcements/announcements.json`，新增一条 `id` 唯一、`published_at` 为当前时间的公告；
2. 带上 `min_version` / `max_version`（例如 `0.1.0`～`0.2.0`）与 `end_at`；
3. 提交到 `main`（同样走 PR），GitHub raw 通常 5 分钟内生效；
4. 本地预览：把同样的 JSON 放到程序目录 `announcements\announcements.json`，重启应用即可看到弹窗。

字段与规则见 [`announcements/README.md`](../announcements/README.md)。

## 6. 常见问题

| 现象 | 原因 / 处理 |
|---|---|
| `ISCC not found` | 安装 Inno Setup 6；只出绿色版时可直接用 ZIP |
| `7-Zip not found` | 安装 7-Zip（默认路径 `C:\Program Files\7-Zip`） |
| 安装包版本号不对 | 确认 `Directory.Build.props` 的 `<Version>` 符合 `X.Y.Z` / `X.Y.Z-alpha.N` / `X.Y.Z-beta.N`；`build\get-version.ps1 -Format Numeric` 应返回 `X.Y.Z.0` |
| 绿色版打不开 | 确认解压完整（`EasiLoginer\` 目录下应有大量 dll），不要只拷贝 exe |
| 推送 main 被拒（GH013） | `main` 要求走 PR：提交到分支 → PR → Squash merge；合并后 `git reset --hard origin/main` |
| 发布包被 SmartScreen 拦截 | 未做代码签名，属预期；可在 Release 说明里提示「更多信息 → 仍要运行」 |

## 7. 回滚

- 删除错误 Release：网页 Release 页 `Delete`；
- 删除错误标签：`git push origin :refs/tags/v0.2.0-alpha.1`（本地 `git tag -d v0.2.0-alpha.1`）；
- 已发布版本不回滚安装包内容，直接发下一个补丁版本。

# vendors/

随包分发的第三方二进制在这里，**不要**提交上游作者的品牌素材。

| 文件 | 来源 | 说明 |
|---|---|---|
| `SeewoPipeBridge.dll` | 上游 EasiAuto（GPLv3）的 `vendors/`，经 Git LFS 分发 | 修补希沃白板时部署的管道桥。**22 016 字节**，x86 程序集（希沃是 32 位），SHA256 `73e3147134ff6c636cc9f57f8d98d3be6b76990e3bbf069da9f9550df95d35b4`（与上游 LFS oid 一致） |
| `DLLPatcher/` | 本仓库 `src/EasiLoginer.Patcher` 发布产物 | 由 `build\publish.cmd` 自动生成，不入库 |

## 关于 SeewoPipeBridge.dll

- 上游用 Git LFS 存放。直接 `git clone` 不带 LFS 时拿到的是 130 字节指针（以 `version https://git-lfs.github.com/spec/v1` 开头），**不能直接用**。
- 获取真实文件的方式：`git lfs pull`，或从 `https://media.githubusercontent.com/media/hxabcd/EasiAuto/master/vendors/SeewoPipeBridge.dll` 直接下载。
- 它是 **x86** 程序集，x64 进程（如 PowerShell）加载会报 `BadImageFormatException`，这是正常现象——它只被注入 32 位的希沃进程。

## 缺失时的表现

若缺少该文件：

- 应用可以正常启动、配置、档案、固定位置 / UIA / 图像识别登录都不受影响；
- 只有「修补希沃」与「令牌投递」会失败并提示缺少文件。

发布前请确认大小是 22 KB 左右，不是 130 字节的 LFS 指针。

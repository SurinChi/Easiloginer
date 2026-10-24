# vendors/

随包分发的第三方二进制在这里，**不要**提交上游作者的品牌素材。

| 文件 | 来源 | 说明 |
|---|---|---|
| `SeewoPipeBridge.dll` | 上游 EasiAuto（GPLv3）的 `vendors/` | 修补希沃白板时部署的管道桥。上游仓库用 Git LFS 存放，本检出里只是 LFS 指针，需要 `git lfs pull` 或从已安装的 EasiAuto 复制实体文件 |
| `DLLPatcher/` | 本仓库 `src/EasiLoginer.Patcher` 发布产物 | 由 `build\publish.cmd` 自动生成，不入库 |

缺少 `SeewoPipeBridge.dll` 时：

- 应用可以正常启动、配置、档案、固定位置登录都不受影响；
- 但「修补希沃」与「令牌投递」会失败并提示缺少文件。

发布前请确认该文件真实存在（大小约 22 KB，不是 130 字节的 LFS 指针）。

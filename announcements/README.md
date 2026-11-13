# 应用内公告

这里放**唯一**一份对外公告文件：`announcements.json`。
EasiLoginer 启动时会读取它，并在应用里弹出当前有效的公告。

## 1. 发布位置（维护者只需要动这一处）

| 用途 | 位置 |
|---|---|
| 正式投放 | 本仓库 `main` 分支的 `announcements/announcements.json` |
| 应用实际请求 | `https://raw.githubusercontent.com/SurinChi/EasiLoginer/main/announcements/announcements.json` |
| 本地预览 / 离线覆盖 | `<EasiLoginer.exe 所在目录>\announcements\announcements.json`（可选，存在时优先） |

- 修改后提交到 `main` 即完成投放，**不需要重新打包发版**；
- GitHub raw 有 CDN 缓存，通常 5 分钟内生效；
- 应用自动检查的节奏：启动时最多每 6 小时一次；关于页的「查看公告」会立即拉取；
- 发布包**不包含**本地覆盖文件，避免它永远盖住线上公告；只在需要本地调试时手动放入。

## 2. 文件格式

```json
{
  "schema": 1,
  "announcements": [
    {
      "id": "2026-11-v0.1.3",
      "title": "0.1.3 已发布",
      "body": "本次更新：快捷登录横向布局、设置页分类导航、新增应用内公告。\n\n建议在设置页检查一次更新。",
      "severity": "info",
      "published_at": "2026-11-13T20:00:00+08:00",
      "start_at": "2026-11-13T20:00:00+08:00",
      "end_at": "2026-12-31T23:59:59+08:00",
      "min_version": "0.1.0",
      "max_version": "0.2.0",
      "url": "https://github.com/SurinChi/Easiloginer/releases",
      "dismissible": true
    }
  ]
}
```

根节点也可以直接是数组（`[{...}]`）。空文件或 `[]` 表示「没有公告」。

## 3. 字段规则

| 字段 | 必填 | 规则 |
|---|---|---|
| `id` | 是 | 1–128 字符，**发布后不要改**；用户勾选「不再提示」按它持久化 |
| `title` | 是 | 1–200 字符 |
| `body` | 否 | 最多 4000 字符；纯文本，`\n` 换行原样显示，不支持 Markdown/HTML |
| `severity` | 否 | `info`（默认，蓝）/ `warning`（橙）/ `critical`（红），只影响配色 |
| `published_at` | 是* | ISO 8601（如 `2026-11-13T20:00:00+08:00`）；用于排序（新→旧） |
| `start_at` | 否 | 开始展示时间；缺省 = 立即 |
| `end_at` | 否 | 结束展示时间（**不含**）；缺省 = 长期有效 |
| `min_version` | 否 | 最低适用版本（含）；写错格式按「不限制」处理 |
| `max_version` | 否 | 最高适用版本（**不含**）；写错格式按「不限制」处理 |
| `url` | 否 | 详情链接，仅 `http`/`https` |
| `dismissible` | 否 | 默认 `true`；`false` 时该条不能勾选「不再提示」 |

\* 缺少 `published_at` 时会尝试用 `start_at`；两者都没有则该条被忽略。

版本号按 PEP 440 比较，`0.1.3`、`0.1.3b1`、`0.1.3rc1` 都支持。

## 4. 展示规则

1. 只显示满足 `start_at ≤ 现在 < end_at` 且版本落在 `[min_version, max_version)` 内的公告；
2. 已被用户「不再提示」的 `id` 不再出现（记录在 `data/config.json` 的 `Internal.HiddenAnnouncementIds`）；
3. 按 `published_at` 倒序，**一次最多显示 3 条**；
4. 单条公告写坏（缺 `id`/`title`、超长、链接非法）只会跳过该条；整份文件写坏则本次不显示，日志里留一条 warning；
5. 文件大小上限 256 KB，读取超时 5 秒；失败完全不打扰用户。

## 5. 隐私

拉取公告只发一次 `GET` 请求，不携带任何账号、设备或使用信息；不需要公告时把本文件保持为
`{"schema":1,"announcements":[]}` 即可。

## 6. 校验清单（发布前自查）

- [ ] JSON 能被解析（无多余逗号、无注释）
- [ ] `id` 唯一且没有复用旧公告的 id
- [ ] `published_at` 是合法 ISO 8601，带时区
- [ ] `min_version` / `max_version` 能按 PEP 440 解析
- [ ] `body` 里的换行是 `\n` 转义
- [ ] 不需要立刻可见时用 `start_at` 排期，而不是改 `published_at`

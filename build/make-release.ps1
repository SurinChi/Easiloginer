param(
    [Parameter(Mandatory = $true)][string]$Version,
    [Parameter(Mandatory = $true)][string]$Dist,
    [string]$RepoRoot = ''
)

$ErrorActionPreference = 'Stop'

# 构建收尾：生成发布说明 RELEASE-<版本>.md 与校验和清单 SHA256SUMS-<版本>.txt。
# 由 publish.cmd 第 8 步自动调用；也可单独运行：
#   powershell -NoProfile -ExecutionPolicy Bypass -File build\make-release.ps1 -Version 0.2.0-alpha.1 -Dist ..\EasiLoginer-dist
#
# 正文来源：
#   1) 若存在 docs\releases\<版本>.md（人工润色的发布说明），以它为正文，自动追加构建信息与校验和；
#   2) 否则用 git 提交标题生成草稿，发布前再润色。

# 参数默认值里取不到 $PSScriptRoot（Windows PowerShell 5.1 绑定阶段还没设置），放到脚本体里。
if (-not $RepoRoot) {
    $RepoRoot = Split-Path $PSScriptRoot -Parent
}

$zip = Join-Path $Dist "EasiLoginer_$Version.zip"
$setup = Join-Path $Dist "EasiLoginer-Setup-$Version.exe"

foreach ($file in @($zip, $setup)) {
    if (-not (Test-Path -LiteralPath $file)) {
        throw "缺少产物：$file（先运行 build\publish.cmd）"
    }
}

function Get-Sha256([string]$Path) {
    (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
}

function Format-Size([long]$Bytes) {
    if ($Bytes -ge 1MB) {
        return ('{0:0.0} MB' -f ($Bytes / 1MB))
    }

    return ('{0:0} KB' -f ($Bytes / 1KB))
}

function Invoke-Git([string[]]$Arguments) {
    $output = & git -C $RepoRoot @Arguments 2>$null
    if ($LASTEXITCODE -ne 0) {
        return $null
    }

    return ($output | Out-String).Trim()
}

$zipSize = Format-Size (Get-Item -LiteralPath $zip).Length
$setupSize = Format-Size (Get-Item -LiteralPath $setup).Length
$zipHash = Get-Sha256 $zip
$setupHash = Get-Sha256 $setup

$commit = Invoke-Git @('rev-parse', '--short', 'HEAD')
if (-not $commit) {
    $commit = 'unknown'
}

$date = Get-Date -Format 'yyyy-MM-dd'
$buildInfo = "发布日期：$date ｜ 构建提交：$commit"

$hashTable = @"
## 校验和（SHA-256）

| 文件 | 大小 | SHA-256 |
| --- | --- | --- |
| ``EasiLoginer-Setup-$Version.exe`` | $setupSize | ``$setupHash`` |
| ``EasiLoginer_$Version.zip`` | $zipSize | ``$zipHash`` |
"@

$curatedPath = Join-Path $RepoRoot "docs\releases\$Version.md"

if (Test-Path -LiteralPath $curatedPath) {
    $curated = [System.IO.File]::ReadAllText($curatedPath, [System.Text.Encoding]::UTF8).TrimEnd()

    # 把构建信息插到一级标题之后，让 Release 页面开头就能看到日期与提交
    $firstBreak = $curated.IndexOf("`n")
    if ($firstBreak -gt 0) {
        $title = $curated.Substring(0, $firstBreak).TrimEnd()
        $body = $curated.Substring($firstBreak + 1).TrimStart()
        $notes = "$title`n`n$buildInfo`n`n$body`n`n$hashTable`n`n---`n本文件由 ``build\make-release.ps1`` 基于 ``docs\releases\$Version.md`` 生成，构建信息与校验和自动追加。`n"
    }
    else {
        $notes = "$curated`n`n$buildInfo`n`n$hashTable`n`n---`n本文件由 ``build\make-release.ps1`` 基于 ``docs\releases\$Version.md`` 生成，构建信息与校验和自动追加。`n"
    }
}
else {
    $lastTag = Invoke-Git @('describe', '--tags', '--abbrev=0', '--match', 'v*')
    if ($lastTag -eq "v$Version") {
        # 已经打过标签后又重新构建：以上一个标签作为对比范围
        $lastTag = Invoke-Git @('describe', '--tags', '--abbrev=0', '--match', 'v*', "$lastTag^")
    }

    if ($lastTag) {
        $changelog = Invoke-Git @('log', '--no-merges', '--pretty=format:- %s (%h)', "$lastTag..HEAD")
        $compare = "自 $lastTag 以来的变更："
    }
    else {
        $changelog = Invoke-Git @('log', '--no-merges', '--pretty=format:- %s (%h)', '-20')
        $compare = '最近 20 条提交：'
    }

    if (-not $changelog) {
        $changelog = '- （本次没有可列出的提交）'
    }

    $notes = @"
# EasiLoginer $Version

$buildInfo

## 下载

| 文件 | 大小 |
| --- | --- |
| ``EasiLoginer-Setup-$Version.exe`` | $setupSize |
| ``EasiLoginer_$Version.zip`` | $zipSize |

- **安装包**：双击运行，按向导安装到 ``%LOCALAPPDATA%\Programs\EasiLoginer``，无需管理员权限。
- **绿色版**：解压后运行 ``EasiLoginer\EasiLoginer.exe``；配置与档案在程序目录的 ``data\`` 下。

## 更新内容

$compare

$changelog

$hashTable

## 升级说明

- 覆盖安装只替换程序文件，保留 ``data\`` 中的配置与档案；卸载才会删除整个安装目录。
- 发布包未做代码签名：SmartScreen 提示时点「更多信息 → 仍要运行」；开启 Smart App Control 的机器可能直接拦截（``0x800711C7``）。

---
没有找到 ``docs\releases\$Version.md``，本文件由 ``build\make-release.ps1`` 用提交标题生成草稿，发布前请补充面向用户的说明。
"@
}

$sums = @(
    "$zipHash  EasiLoginer_$Version.zip",
    "$setupHash  EasiLoginer-Setup-$Version.exe"
) -join "`r`n"

$utf8 = New-Object System.Text.UTF8Encoding($false)
$notesPath = Join-Path $Dist "RELEASE-$Version.md"
$sumsPath = Join-Path $Dist "SHA256SUMS-$Version.txt"
[System.IO.File]::WriteAllText($notesPath, $notes, $utf8)
[System.IO.File]::WriteAllText($sumsPath, $sums + "`r`n", $utf8)

Write-Output "RELEASE notes : $notesPath"
Write-Output "SHA256SUMS    : $sumsPath"
Write-Output "ZIP           : $zip  ($zipSize)  SHA256=$zipHash"
Write-Output "SETUP         : $setup  ($setupSize)  SHA256=$setupHash"

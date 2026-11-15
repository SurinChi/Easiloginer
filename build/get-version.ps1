param(
    [string]$Props = '',
    [ValidateSet('Full', 'Numeric')][string]$Format = 'Full'
)

# 版本号的唯一来源：Directory.Build.props 里的 <Version>。
# 发布脚本（publish.cmd / installer.iss / make-release.ps1）都用它，避免多处手改漏改。
#
# 约定：
#   稳定版：0.2.0
#   Alpha： 0.2.0-alpha.1
#   Beta：  0.2.0-beta.1
#   -Format Numeric 输出 Windows 文件版本所需的 X.Y.Z.0（去掉 alpha/beta 后缀）。

$ErrorActionPreference = 'Stop'

# 参数默认值里取不到 $PSScriptRoot，放到脚本体里。
if (-not $Props) {
    $Props = Join-Path $PSScriptRoot '..\Directory.Build.props'
}

if (-not (Test-Path -LiteralPath $Props)) {
    throw "找不到 $Props"
}

[xml]$xml = Get-Content -LiteralPath $Props -Raw
$version = $xml.Project.PropertyGroup |
    ForEach-Object { $_.Version } |
    Where-Object { $_ } |
    Select-Object -First 1

if (-not $version) {
    throw "$Props 中没有 <Version>"
}

$version = $version.Trim()

# 只接受稳定版或 Alpha/Beta 测试版；测试序号必须是正整数。
$pattern = '^(?<release>\d+\.\d+\.\d+)(?:-(?<stage>alpha|beta)\.(?<number>[1-9]\d*))?$'
if ($version -notmatch $pattern) {
    throw "版本号格式不符合约定：$version（稳定版 X.Y.Z；测试版 X.Y.Z-alpha.N 或 X.Y.Z-beta.N）"
}

if ($Format -eq 'Numeric') {
    # AssemblyVersion / FileVersion / Inno VersionInfoVersion 都必须是四段数字。
    Write-Output ($Matches['release'] + '.0')
    return
}

Write-Output $version

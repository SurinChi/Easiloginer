param(
    [string]$Props = ''
)

# 版本号的唯一来源：Directory.Build.props 里的 <Version>。
# 发布脚本（publish.cmd / installer.iss / make-release.ps1）都用它，避免多处手改漏改。

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
if ($version -notmatch '^\d+\.\d+\.\d+([-.+][0-9A-Za-z.-]+)?$') {
    throw "版本号格式不符合预期：$version"
}

Write-Output $version

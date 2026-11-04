<#
.SYNOPSIS
    用 7-Zip 生成自解压安装包（LZMA2），体积约为 ZIP 的一半。

.DESCRIPTION
    解压后自动运行 `EasiLoginer.exe --install`，由程序内的安装向导
    选择目标目录、创建快捷方式并写卸载项。
#>
param(
    [Parameter(Mandatory = $true)][string]$Stage,
    [Parameter(Mandatory = $true)][string]$Out,
    [Parameter(Mandatory = $true)][string]$Version,
    [string]$SevenZip = "C:\Program Files\7-Zip\7z.exe"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $SevenZip)) { throw "未找到 7-Zip：$SevenZip" }
if (-not (Test-Path $Stage)) { throw "负载目录不存在：$Stage" }

$stagePath = (Resolve-Path $Stage).Path
$outPath = [System.IO.Path]::GetFullPath($Out)
$work = Join-Path ([System.IO.Path]::GetTempPath()) ("easi-sfx-" + [guid]::NewGuid().ToString("N").Substring(0, 8))
$payload = Join-Path $work "payload"
$archive = Join-Path $work "payload.7z"
$config = Join-Path $work "config.txt"
$sfx = Join-Path (Split-Path $SevenZip) "7z.sfx"

if (-not (Test-Path $sfx)) { throw "未找到 7z.sfx：$sfx" }

New-Item -ItemType Directory -Path $payload -Force | Out-Null
try {
    Write-Host "  copying payload..."
    Copy-Item (Join-Path $stagePath "*") $payload -Recurse -Force

    $sfxConfig = @"
;!@Install@!UTF-8!
Title="EasiLoginer $Version"
BeginPrompt="即将解压 EasiLoginer $Version，解压完成后会自动打开安装向导。是否继续？"
ExtractDialogText="请选择解压位置（安装向导随后会让你选择正式安装目录）"
RunProgram="EasiLoginer.exe install"
;!@InstallEnd@!
"@
    [System.IO.File]::WriteAllText($config, $sfxConfig.TrimStart(), [System.Text.Encoding]::UTF8)

    Write-Host "  compressing (LZMA2)..."
    & $SevenZip a -t7z -mx=9 -m0=lzma2 -bsp0 -bso0 $archive (Join-Path $payload "*") | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "7z 压缩失败（$LASTEXITCODE）" }

    Write-Host "  assembling SFX..."
    $outStream = [System.IO.File]::Create($outPath)
    try {
        foreach ($part in @($sfx, $config, $archive)) {
            $bytes = [System.IO.File]::ReadAllBytes($part)
            $outStream.Write($bytes, 0, $bytes.Length)
        }
    }
    finally {
        $outStream.Dispose()
    }

    Write-Host ("SFX written: {0} ({1:N2} MB)" -f $outPath, ((Get-Item $outPath).Length / 1MB))
}
finally {
    Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue
}

<#
.SYNOPSIS
    用 7-Zip 生成自解压安装包（LZMA2），体积约为 ZIP 的一半。

.DESCRIPTION
    相比内嵌 ZIP 的 WPF 向导（ZIP + 自带运行时 ≈ 173 MB），SFX 方案
    只压缩负载本身（≈55 MB），解压后运行 install.cmd 创建快捷方式与卸载项。
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

    # 安装脚本：创建快捷方式、卸载项，并启动程序
    $installPs1 = @'
$ErrorActionPreference = "Stop"
$dir = $PSScriptRoot
$exe = Join-Path $dir "EasiLoginer.exe"
if (-not (Test-Path $exe)) { throw "EasiLoginer.exe missing" }

$desktop = [Environment]::GetFolderPath("DesktopDirectory")
$programs = [Environment]::GetFolderPath("Programs")
$shell = New-Object -ComObject WScript.Shell

foreach ($target in @((Join-Path $desktop "EasiLoginer.lnk"), (Join-Path $programs "EasiLoginer.lnk"))) {
    $link = $shell.CreateShortcut($target)
    $link.TargetPath = $exe
    $link.WorkingDirectory = $dir
    $link.IconLocation = $exe
    $link.Description = "EasiLoginer"
    $link.Save()
}

$uninstall = Join-Path $dir "Uninstall.cmd"
$lines = @(
    "@echo off",
    "reg delete `"HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\EasiLoginer`" /f >nul 2>&1",
    "del /q `"$desktop\EasiLoginer.lnk`" >nul 2>&1",
    "del /q `"$programs\EasiLoginer.lnk`" >nul 2>&1",
    "start `"`" /min cmd /c `"timeout /t 2 >nul & rd /s /q `"`"%~dp0`"`"`"",
    "exit /b 0"
)
[System.IO.File]::WriteAllText($uninstall, ($lines -join "`r`n") + "`r`n", [System.Text.Encoding]::ASCII)

$version = (Get-Item $exe).VersionInfo.ProductVersion
$key = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\EasiLoginer"
New-Item -Path $key -Force | Out-Null
Set-ItemProperty $key DisplayName "EasiLoginer"
Set-ItemProperty $key DisplayVersion $version
Set-ItemProperty $key Publisher "SurinChi"
Set-ItemProperty $key InstallLocation $dir
Set-ItemProperty $key DisplayIcon $exe
Set-ItemProperty $key UninstallString ('"' + $uninstall + '"')
Set-ItemProperty $key NoModify 1 -Type DWord
Set-ItemProperty $key NoRepair 1 -Type DWord

Start-Process -FilePath $exe -WorkingDirectory $dir
'@
    $installCmd = @"
@echo off
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1"
exit /b 0
"@

    [System.IO.File]::WriteAllText((Join-Path $payload "install.ps1"), $installPs1, (New-Object System.Text.UTF8Encoding($true)))
    [System.IO.File]::WriteAllText((Join-Path $payload "install.cmd"), $installCmd, [System.Text.Encoding]::ASCII)

    $sfxConfig = @"
;!@Install@!UTF-8!
Title="EasiLoginer $Version"
BeginPrompt="安装 EasiLoginer $Version 到所选目录？"
RunProgram="install.cmd"
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

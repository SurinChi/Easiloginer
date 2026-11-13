@echo off
rem EasiLoginer publish script (doc 12.1): self-contained win-x64 + portable layout + ZIP + self-extracting installer.
rem Output goes OUTSIDE the repository (parent folder) so the worktree stays clean.
setlocal
if exist "%USERPROFILE%\.dotnet\dotnet.exe" set "PATH=%USERPROFILE%\.dotnet;%PATH%"

set "ROOT=%~dp0.."
set "DIST=%ROOT%\..\EasiLoginer-dist"
set "OUT=%DIST%\out"
set "STAGE=%DIST%\stage\EasiLoginer"
set "ZIP=%DIST%\EasiLoginer_0.1.3.zip"
set "SETUP_OUT=%DIST%\setup"
set "SETUP=%DIST%\EasiLoginer-Setup-0.1.3.exe"
set "PAYLOAD=%DIST%\payload.7z"
set "TOOLS=%DIST%\tools"

if exist "%OUT%" rmdir /s /q "%OUT%"
if exist "%STAGE%" rmdir /s /q "%STAGE%"
if exist "%SETUP_OUT%" rmdir /s /q "%SETUP_OUT%"
if exist "%ZIP%" del /q "%ZIP%"
if exist "%SETUP%" del /q "%SETUP%"
if exist "%PAYLOAD%" del /q "%PAYLOAD%"
if exist "%TOOLS%" rmdir /s /q "%TOOLS%"
if not exist "%DIST%" mkdir "%DIST%"

echo [1/7] publish main app
dotnet publish "%ROOT%\src\EasiLoginer.Host\EasiLoginer.Host.csproj" -c Release -r win-x64 --self-contained true -p:PublishReadyToRun=true -p:DebugType=embedded -p:PublishSingleFile=false -p:PublishTrimmed=false -p:Version=0.1.3 -p:AssemblyInformationalVersion=0.1.3 -o "%OUT%"
if errorlevel 1 exit /b 1

echo [2/7] assemble portable folder
mkdir "%STAGE%" 2>nul
xcopy /e /i /q /y "%OUT%" "%STAGE%" >nul
copy /y "%ROOT%\LICENSE" "%STAGE%\" >nul
copy /y "%ROOT%\NOTICE" "%STAGE%\" >nul
copy /y "%ROOT%\THIRD-PARTY-NOTICES.md" "%STAGE%\" >nul
copy /y "%ROOT%\README.md" "%STAGE%\" >nul

echo [3/7] publish DllPatcher to vendors\DLLPatcher
dotnet publish "%ROOT%\src\EasiLoginer.Patcher\EasiLoginer.Patcher.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:DebugType=embedded -o "%STAGE%\vendors\DLLPatcher"
if errorlevel 1 exit /b 1

echo [4/7] copy pipe bridge if present
if exist "%ROOT%\vendors\SeewoPipeBridge.dll" (
  copy /y "%ROOT%\vendors\SeewoPipeBridge.dll" "%STAGE%\vendors\" >nul
  echo   SeewoPipeBridge.dll copied
) else (
  echo   WARNING: vendors\SeewoPipeBridge.dll not found; patching will report "bridge missing"
)

echo [5/7] build ZIP (forward slashes via .NET ZipFile)
powershell -NoProfile -ExecutionPolicy Bypass -File "%ROOT%\build\make-zip.ps1" -Stage "%STAGE%" -Zip "%ZIP%"
if errorlevel 1 exit /b 1

echo [6/7] build payload archive (LZMA2) and bundle 7z tools
if not exist "%ProgramFiles%\7-Zip\7z.exe" (
  echo   ERROR: 7-Zip not found. Install 7-Zip to build the installer.
  echo   The portable ZIP is ready at %ZIP%
  exit /b 1
)
"%ProgramFiles%\7-Zip\7z.exe" a -t7z -mx=9 -m0=lzma2 "%PAYLOAD%" "%STAGE%\*" -bsp0 -bso0
if errorlevel 1 exit /b 1
mkdir "%TOOLS%" 2>nul
copy /y "%ProgramFiles%\7-Zip\7z.exe" "%TOOLS%\" >nul
copy /y "%ProgramFiles%\7-Zip\7z.dll" "%TOOLS%\" >nul

echo [7/7] build installer with Inno Setup
set "ISCC=%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe"
if not exist "%ISCC%" set "ISCC=%ProgramFiles%\Inno Setup 6\ISCC.exe"
if not exist "%ISCC%" (
  echo   ERROR: Inno Setup 6 not found. Install it to build the installer.
  echo   The portable ZIP is ready at %ZIP%
  exit /b 1
)
"%ISCC%" "%ROOT%\build\installer.iss"
if errorlevel 1 exit /b 1
echo Done: %ZIP%
echo Done: %SETUP%
exit /b 0

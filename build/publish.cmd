@echo off
rem EasiLoginer publish script (doc 12.1): self-contained win-x64 + portable layout + ZIP.
rem Output goes OUTSIDE the repository (parent folder) so the worktree stays clean.
setlocal
if exist "%USERPROFILE%\.dotnet\dotnet.exe" set "PATH=%USERPROFILE%\.dotnet;%PATH%"

set "ROOT=%~dp0.."
set "DIST=%ROOT%\..\EasiLoginer-dist"
set "OUT=%DIST%\out"
set "STAGE=%DIST%\stage\EasiLoginer"
set "ZIP=%DIST%\EasiLoginer_0.0.1.zip"

if exist "%OUT%" rmdir /s /q "%OUT%"
if exist "%STAGE%" rmdir /s /q "%STAGE%"
if exist "%ZIP%" del /q "%ZIP%"
if not exist "%DIST%" mkdir "%DIST%"

echo [1/5] publish main app
dotnet publish "%ROOT%\src\EasiLoginer.Host\EasiLoginer.Host.csproj" -c Release -r win-x64 --self-contained true -p:PublishReadyToRun=true -p:DebugType=embedded -p:PublishSingleFile=false -p:PublishTrimmed=false -p:Version=0.0.1 -p:AssemblyInformationalVersion=0.0.1 -o "%OUT%"
if errorlevel 1 exit /b 1

echo [2/5] assemble portable folder
mkdir "%STAGE%" 2>nul
xcopy /e /i /q /y "%OUT%" "%STAGE%" >nul
copy /y "%ROOT%\LICENSE" "%STAGE%\" >nul
copy /y "%ROOT%\NOTICE" "%STAGE%\" >nul
copy /y "%ROOT%\THIRD-PARTY-NOTICES.md" "%STAGE%\" >nul
copy /y "%ROOT%\README.md" "%STAGE%\" >nul

echo [3/5] publish DllPatcher to vendors\DLLPatcher
dotnet publish "%ROOT%\src\EasiLoginer.Patcher\EasiLoginer.Patcher.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:DebugType=embedded -o "%STAGE%\vendors\DLLPatcher"
if errorlevel 1 exit /b 1

echo [4/5] copy pipe bridge if present
if exist "%ROOT%\vendors\SeewoPipeBridge.dll" (
  copy /y "%ROOT%\vendors\SeewoPipeBridge.dll" "%STAGE%\vendors\" >nul
  echo   SeewoPipeBridge.dll copied
) else (
  echo   WARNING: vendors\SeewoPipeBridge.dll not found; patching will report "bridge missing"
)

echo [5/5] build ZIP (forward slashes via .NET ZipFile)
powershell -NoProfile -ExecutionPolicy Bypass -File "%ROOT%\build\make-zip.ps1" -Stage "%STAGE%" -Zip "%ZIP%"
if errorlevel 1 exit /b 1

echo Done: %ZIP%
exit /b 0

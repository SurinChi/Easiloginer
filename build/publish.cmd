@echo off
rem EasiLoginer publish script (doc 12.1): self-contained win-x64 + portable layout + ZIP + self-extracting installer.
rem Output goes OUTSIDE the repository (parent folder) so the worktree stays clean.
setlocal
if exist "%USERPROFILE%\.dotnet\dotnet.exe" set "PATH=%USERPROFILE%\.dotnet;%PATH%"

set "ROOT=%~dp0.."
set "DIST=%ROOT%\..\EasiLoginer-dist"
set "OUT=%DIST%\out"
set "STAGE=%DIST%\stage\EasiLoginer"
set "ZIP=%DIST%\EasiLoginer_0.0.4.zip"
set "SETUP_OUT=%DIST%\setup"
set "SETUP=%DIST%\EasiLoginer-Setup-0.0.4.exe"

if exist "%OUT%" rmdir /s /q "%OUT%"
if exist "%STAGE%" rmdir /s /q "%STAGE%"
if exist "%SETUP_OUT%" rmdir /s /q "%SETUP_OUT%"
if exist "%ZIP%" del /q "%ZIP%"
if exist "%SETUP%" del /q "%SETUP%"
if not exist "%DIST%" mkdir "%DIST%"

echo [1/6] publish main app
dotnet publish "%ROOT%\src\EasiLoginer.Host\EasiLoginer.Host.csproj" -c Release -r win-x64 --self-contained true -p:PublishReadyToRun=true -p:DebugType=embedded -p:PublishSingleFile=false -p:PublishTrimmed=false -p:Version=0.0.4 -p:AssemblyInformationalVersion=0.0.4 -o "%OUT%"
if errorlevel 1 exit /b 1

echo [2/6] assemble portable folder
mkdir "%STAGE%" 2>nul
xcopy /e /i /q /y "%OUT%" "%STAGE%" >nul
copy /y "%ROOT%\LICENSE" "%STAGE%\" >nul
copy /y "%ROOT%\NOTICE" "%STAGE%\" >nul
copy /y "%ROOT%\THIRD-PARTY-NOTICES.md" "%STAGE%\" >nul
copy /y "%ROOT%\README.md" "%STAGE%\" >nul

echo [3/6] publish DllPatcher to vendors\DLLPatcher
dotnet publish "%ROOT%\src\EasiLoginer.Patcher\EasiLoginer.Patcher.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:DebugType=embedded -o "%STAGE%\vendors\DLLPatcher"
if errorlevel 1 exit /b 1

echo [4/6] copy pipe bridge if present
if exist "%ROOT%\vendors\SeewoPipeBridge.dll" (
  copy /y "%ROOT%\vendors\SeewoPipeBridge.dll" "%STAGE%\vendors\" >nul
  echo   SeewoPipeBridge.dll copied
) else (
  echo   WARNING: vendors\SeewoPipeBridge.dll not found; patching will report "bridge missing"
)

echo [5/6] build ZIP (forward slashes via .NET ZipFile)
powershell -NoProfile -ExecutionPolicy Bypass -File "%ROOT%\build\make-zip.ps1" -Stage "%STAGE%" -Zip "%ZIP%"
if errorlevel 1 exit /b 1

echo [6/6] build self-extracting installer
dotnet publish "%ROOT%\src\EasiLoginer.Installer\EasiLoginer.Installer.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=embedded -p:PayloadZip="%ZIP%" -p:Version=0.0.4 -p:AssemblyInformationalVersion=0.0.4 -o "%SETUP_OUT%"
if errorlevel 1 exit /b 1
copy /y "%SETUP_OUT%\EasiLoginer-Setup.exe" "%SETUP%" >nul
if errorlevel 1 exit /b 1

echo Done: %ZIP%
echo Done: %SETUP%
exit /b 0

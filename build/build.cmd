@echo off
rem EasiLoginer 构建入口（Windows 默认的 PowerShell 执行策略禁止运行 .ps1，故用 .cmd）
rem 用法：build\build.cmd [额外的 dotnet build 参数]
setlocal
if exist "%USERPROFILE%\.dotnet\dotnet.exe" set "PATH=%USERPROFILE%\.dotnet;%PATH%"
dotnet build "%~dp0..\EasiLoginer.sln" -c Release %*
exit /b %ERRORLEVEL%

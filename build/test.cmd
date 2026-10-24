@echo off
rem EasiLoginer 构建 + 测试入口。
rem 用法：build\test.cmd [额外的 dotnet test 参数]
setlocal
if exist "%USERPROFILE%\.dotnet\dotnet.exe" set "PATH=%USERPROFILE%\.dotnet;%PATH%"
dotnet build "%~dp0..\EasiLoginer.sln" -c Release
if errorlevel 1 exit /b %ERRORLEVEL%
dotnet test "%~dp0..\EasiLoginer.sln" -c Release --no-build %*
exit /b %ERRORLEVEL%

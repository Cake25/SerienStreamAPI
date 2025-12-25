@echo off
setlocal

rem Force UTF-8 code page for consistent Terminal.Gui rendering on Windows
chcp 65001 >NUL

pushd "%~dp0"

rem Ensure the .NET SDK is available
where dotnet >NUL 2>&1
if errorlevel 1 (
    echo The .NET 8 SDK is required to run the terminal GUI. Download it from https://dotnet.microsoft.com/download/dotnet/8.0
    popd
    exit /b 1
)

dotnet run --project "SerienStreamAPI.Gui" %*
set exit_code=%ERRORLEVEL%

popd
exit /b %exit_code%

@echo off
rem Desk Rhythm: run the tests and build dist\DeskRhythm.exe (one self-contained file).
rem Double-click this file, or run it from a terminal. No admin rights needed.
setlocal
cd /d "%~dp0"

set "DOTNET="
call :try_dotnet dotnet
if not defined DOTNET call :try_dotnet "%LOCALAPPDATA%\Microsoft\dotnet\dotnet.exe"
if not defined DOTNET call :try_dotnet "%ProgramFiles%\dotnet\dotnet.exe"
if not defined DOTNET (
    echo.
    echo The .NET 10 SDK was not found.
    echo Install it from https://dotnet.microsoft.com/download/dotnet/10.0
    echo or, without admin rights, run this in PowerShell:
    echo   Invoke-WebRequest https://dot.net/v1/dotnet-install.ps1 -OutFile dotnet-install.ps1
    echo   .\dotnet-install.ps1 -Channel 10.0 -InstallDir "$env:LOCALAPPDATA\Microsoft\dotnet"
    echo Then run build.cmd again.
    goto :fail
)

set DOTNET_CLI_TELEMETRY_OPTOUT=1
set DOTNET_NOLOGO=1

echo === Running unit tests ===
%DOTNET% test tests\DeskRhythm.Core.Tests -c Release --nologo -v quiet
if errorlevel 1 goto :fail

echo.
echo === Building dist\DeskRhythm.exe ===
%DOTNET% publish src\DeskRhythm\DeskRhythm.csproj -c Release -r win-x64 --self-contained true ^
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none ^
    -o dist --nologo -v quiet
if errorlevel 1 goto :fail

echo.
echo Done: %CD%\dist\DeskRhythm.exe
echo Copy it anywhere (for example %%LOCALAPPDATA%%\Programs\DeskRhythm) and double-click it.
if "%~1"=="" pause
exit /b 0

:try_dotnet
%1 --list-sdks 2>nul | findstr /b "10." >nul && set "DOTNET=%1"
exit /b 0

:fail
echo.
echo Build failed.
if "%~1"=="" pause
exit /b 1

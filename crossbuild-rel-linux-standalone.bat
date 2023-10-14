@echo off
SETLOCAL
REM %~dp0 is a batch script implicit variable.
REM The variable is a string which is the full path to THE DIRECTORY WHICH CONTAINS THIS BATCH SCRIPT.
@cd /d %~dp0

SET "DOTNET_CLI_TELEMETRY_OPTOUT=1"
SET "MSBUILDDISABLENODEREUSE=1"

REM If folder "sdk" exist here, use the local sdk instead.
IF EXIST "%~dp0\sdk" (
 SET "DOTNET_ROOT=%~dp0\sdk"
 SET "PATH=%DOTNET_ROOT%;%PATH%"
)

REM You can switch between:
REM - Non-windows or Linux: "net7.0" or "net8.0"
REM You can also switch to .NET8 SDK by changing the "net7.0" to "net8.0" value of the "-f" argument

REM As of writing this script, select .NET7 because CsWin32 doesn't generate compatible code with .NET8 SDK v1.0.0-rc1
dotnet.exe publish -r linux-x64 -f net8.0 --self-contained -c Release -p:PublishAot=false -p:PublishReadyToRun=true -p:PublishSingleFile=true -p:PublishTrimmed=true -o "release\linux\standalone" "src\SnowBreakLauncher.csproj" 

ENDLOCAL
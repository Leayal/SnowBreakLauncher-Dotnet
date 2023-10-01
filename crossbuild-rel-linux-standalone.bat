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

dotnet.exe publish -r linux-x64 --self-contained -c Release -p:PublishAot=false -p:PublishReadyToRun=true -p:PublishSingleFile=true -p:PublishTrimmed=true -o "release\linux" "src\SnowBreakLauncher.csproj" 

ENDLOCAL
@echo off
SETLOCAL
@cd /d %~dp0

SET "DOTNET_CLI_TELEMETRY_OPTOUT=1"
SET "MSBUILDDISABLENODEREUSE=1"

REM If folder "sdk" exist here, use the local sdk instead.
IF EXIST "%~dp0\sdk" (
 SET "DOTNET_ROOT=%~dp0\sdk"
 SET "PATH=%DOTNET_ROOT%;%PATH%"
)

dotnet.exe publish -c Release -o "release" "src\SnowBreakLauncher.csproj" 

ENDLOCAL
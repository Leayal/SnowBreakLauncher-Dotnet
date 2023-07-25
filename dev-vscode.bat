@echo off
SETLOCAL
@cd /d %~dp0
for /f "delims=" %%i in ('where code.cmd') do set "VSCodeCliPath=%%i"
SET "VSCODEPATH=%VSCodeCliPath%\..\.."

REM If folder "sdk" exist here, use the local sdk instead.
IF EXIST "%~dp0\sdk" (
 SET "DOTNET_ROOT=%~dp0\sdk"
 SET "PATH=%DOTNET_ROOT%;%VSCODEPATH%;%PATH%"
)

start "" "%VSCODEPATH%\code.exe" "%~dp0"
ENDLOCAL
exit
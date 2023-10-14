#!/bin/bash

SCRIPT_DIR="$(dirname "$(readlink -f "$0")")"

cd "$SCRIPT_DIR"

DOTNET_CLI_LOCAL="$SCRIPT_DIR""/sdk/dotnet"
DOTNET_CLI="dotnet"
if test -f $DOTNET_CLI_LOCAL;
then
    DOTNET_CLI="$DOTNET_CLI_LOCAL"
fi

export DOTNET_CLI_TELEMETRY_OPTOUT="1"
export MSBUILDDISABLENODEREUSE="1"

# You can switch between:
# - Non-windows or Linux: "net7.0" or "net8.0"
# You can also switch to .NET8 SDK by changing the "net7.0" to "net8.0" value of the "-f" argument

# As of writing this script, select .NET7 because CsWin32 doesn't generate compatible code with .NET8 SDK v1.0.0-rc1
# As of writing this, I don't know how to get NativeAOT working on Linux for this. So we're not using AOT compilation for now, using default IL binaries.
"$DOTNET_CLI" publish -r linux-x64 -f net8.0 --self-contained -c Release -p:PublishAot=false -p:PublishReadyToRun=true -p:PublishSingleFile=true -p:PublishTrimmed=true -o "$SCRIPT_DIR""/release/linux/standalone" "$SCRIPT_DIR""/src/SnowBreakLauncher.csproj" 

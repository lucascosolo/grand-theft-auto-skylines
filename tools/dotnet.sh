#!/usr/bin/env bash
# The project's dotnet: SDK 10 from ~/.cache/dotnet/sdk10 with every cache under ~/.cache (the sandbox cannot write ~/.dotnet or ~/.nuget).
set -euo pipefail
export DOTNET_CLI_HOME="${DOTNET_CLI_HOME:-$HOME/.cache/gta-skylines/dotnet-home}"
# Shared with the reference project so offline restores find xunit and the reference assemblies package.
export NUGET_PACKAGES="${NUGET_PACKAGES:-$HOME/.cache/minecraft-skylines/nuget}"
# NuGet's first-run migration needs a named mutex under /tmp; marker files under XDG_DATA_HOME skip it.
export XDG_DATA_HOME="${XDG_DATA_HOME:-$HOME/.cache/gta-skylines/xdg-data}"
mkdir -p "${XDG_DATA_HOME:?}/NuGet/Migrations" && touch "${XDG_DATA_HOME:?}/NuGet/Migrations/"{1,2,3}
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 MSBUILDDISABLENODEREUSE=1 DOTNET_CLI_USE_MSBUILD_SERVER=0
exec "${DOTNET:-$HOME/.cache/dotnet/sdk10/dotnet}" "$@"

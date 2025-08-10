#!/usr/bin/env bash
# Sets up a new shell session to use the bootstrap version of MSBuild
# Usage: source ./scripts/use-bootstrap-msbuild.sh

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(dirname "$SCRIPT_DIR")"
BOOTSTRAP_PATH="$REPO_ROOT/artifacts/bin/bootstrap/core/"

# Prepend bootstrap path to PATH
export PATH="$BOOTSTRAP_PATH:$PATH"

# Set DOTNET_ROOT to bootstrap path
export DOTNET_ROOT="$BOOTSTRAP_PATH"

# Set BuildWithNetFrameworkHostedCompiler to false
export BuildWithNetFrameworkHostedCompiler=false

# Set DOTNET_SYSTEM_NET_SECURITY_NOREVOCATIONCHECKBYDEFAULT to true
export DOTNET_SYSTEM_NET_SECURITY_NOREVOCATIONCHECKBYDEFAULT=true

# Set shell prompt/window title (works in most terminals)
echo -ne "\033]0;MSBuild dogfood\007"

echo "Bootstrap MSBuild environment configured."

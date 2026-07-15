#!/usr/bin/env bash

_COPILOT_GH_HOOK_DIR="$(
    cd "$(dirname "${BASH_SOURCE[0]}")" >/dev/null 2>&1 &&
        pwd -P
)"
_COPILOT_GH_REAL="$(type -P gh 2>/dev/null || true)"
_COPILOT_GH_PYTHON="$(command -v python3 2>/dev/null || true)"
_COPILOT_GH_WRAPPER="$_COPILOT_GH_HOOK_DIR/gh-comment-wrapper.py"

if [[ -n "$_COPILOT_GH_REAL" && -n "$_COPILOT_GH_PYTHON" && -f "$_COPILOT_GH_WRAPPER" ]]; then
    gh() {
        "$_COPILOT_GH_PYTHON" "$_COPILOT_GH_WRAPPER" "$_COPILOT_GH_REAL" "$@"
    }
fi

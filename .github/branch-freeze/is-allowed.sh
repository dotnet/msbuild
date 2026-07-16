#!/usr/bin/env bash
# Exits 0 if <actor-login> appears in the branch-freeze allowlist, non-zero otherwise.
#
# Usage:   is-allowed.sh <actor-login> [allowlist-path]
#          allowlist-path defaults to .github/branch-freeze-allowlist.txt
#
# The allowlist has one GitHub login per line; blank lines and `#` comments are
# ignored, matching is case-insensitive, and surrounding whitespace is trimmed.
# Exit codes: 0 = allowed, 1 = not allowed, 2 = allowlist file missing.
set -uo pipefail

actor="${1:?actor login required}"
allowlist="${2:-.github/branch-freeze-allowlist.txt}"

if [ ! -f "$allowlist" ]; then
  echo "Allowlist file '$allowlist' not found." >&2
  exit 2
fi

actor_lc="$(printf '%s' "$actor" | tr '[:upper:]' '[:lower:]')"
while IFS= read -r line || [ -n "$line" ]; do
  login="${line%%#*}"                                   # strip trailing comment
  login="$(printf '%s' "$login" | tr -d '[:space:]')"   # trim all whitespace
  [ -n "$login" ] || continue
  if [ "$(printf '%s' "$login" | tr '[:upper:]' '[:lower:]')" = "$actor_lc" ]; then
    exit 0
  fi
done < "$allowlist"

exit 1

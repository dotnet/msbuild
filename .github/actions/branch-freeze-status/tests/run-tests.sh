#!/usr/bin/env bash
# Security-focused unit tests for the branch-freeze authorization and status logic.
#
# These pin the two behaviors that matter most if they silently break:
#   * is-allowed.sh         - who may run /freeze /unfreeze (deny-by-default).
#   * post-freeze-status.sh - a branch is frozen ONLY when an open issue carries the
#                             marker on a WHOLE LINE, so a mere mention cannot freeze.
#
# Uses a mock `gh` (tests/mock-gh) on PATH; requires real `jq` (as the scripts do).
# Run:  bash .github/actions/branch-freeze-status/tests/run-tests.sh
set -uo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
action_dir="$(cd "$here/.." && pwd)"
repo_root="$(cd "$action_dir/../../.." && pwd)"

if ! command -v jq >/dev/null 2>&1; then
  echo "SKIP: 'jq' is not available; these tests require jq (as the scripts do)."
  exit 0
fi

mockbin="$(mktemp -d)"
cp "$here/mock-gh" "$mockbin/gh"
chmod +x "$mockbin/gh"
export PATH="$mockbin:$PATH"

pass=0
fail=0
ok()  { pass=$((pass + 1)); printf '  ok   - %s\n' "$1"; }
bad() { fail=$((fail + 1)); printf '  FAIL - %s\n' "$1"; [ -n "${2:-}" ] && printf '         %s\n' "$2"; }
assert_eq() { if [ "$1" = "$2" ]; then ok "$3"; else bad "$3" "expected [$2] got [$1]"; fi; }

status_field() { grep -E "^$1=" "$GH_STATUS_FILE" | head -n1 | cut -d= -f2-; }
fresh_status() { GH_STATUS_FILE="$(mktemp)"; export GH_STATUS_FILE; : > "$GH_STATUS_FILE"; }

echo "== is-allowed.sh (authorization boundary) =="
allow="$repo_root/.github/branch-freeze-allowlist.txt"
bash "$action_dir/is-allowed.sh" "rainersigwald"   "$allow" && ok "listed login is allowed"          || bad "listed login is allowed"
bash "$action_dir/is-allowed.sh" "RAINERSIGWALD"   "$allow" && ok "match is case-insensitive"         || bad "match is case-insensitive"
bash "$action_dir/is-allowed.sh" "not-a-real-user" "$allow" && bad "unknown login is denied"          || ok  "unknown login is denied"

empty="$(mktemp)"; printf '# only a comment\n\n' > "$empty"
bash "$action_dir/is-allowed.sh" "anyone" "$empty"          && bad "empty allowlist denies (deny-by-default)" || ok "empty allowlist denies (deny-by-default)"
bash "$action_dir/is-allowed.sh" "anyone" "$empty.missing"; [ "$?" -eq 2 ] && ok "missing allowlist file denies (exit 2)" || bad "missing allowlist file denies (exit 2)"

echo "== post-freeze-status.sh (freeze detection) =="
export REPO="o/r"

fresh_status
MOCK_ISSUES='[]' bash "$action_dir/post-freeze-status.sh" "sha-open" "main"
assert_eq "$(status_field state)" "success" "no tracking issue -> branch open (success)"

fresh_status
MOCK_ISSUES='[{"number":7,"url":"https://github.com/o/r/issues/7","body":"SDK insertion broke\n\n<!-- branch-freeze:main -->\n<!-- branch-freeze-by:rainersigwald -->"}]' \
  bash "$action_dir/post-freeze-status.sh" "sha-frozen" "main"
assert_eq "$(status_field state)" "failure" "whole-line marker -> branch frozen (failure)"
assert_eq "$(status_field description)" "Frozen by @rainersigwald: SDK insertion broke" "status names who froze it"
assert_eq "$(status_field target_url)" "https://github.com/o/r/issues/7" "status links the tracking issue"

fresh_status
MOCK_ISSUES='[{"number":9,"url":"u","body":"heads up: <!-- branch-freeze:main --> is the marker"}]' \
  bash "$action_dir/post-freeze-status.sh" "sha-mention" "main"
assert_eq "$(status_field state)" "success" "marker mentioned mid-line does NOT freeze"

echo
echo "Passed: $pass   Failed: $fail"
[ "$fail" -eq 0 ]

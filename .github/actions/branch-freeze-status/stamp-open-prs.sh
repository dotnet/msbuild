#!/usr/bin/env bash
# Bulk-stamps the `branch-freeze` status on every open pull request, optionally
# limited to a single base branch. Calls post-freeze-status.sh once per PR and
# aggregates failures into a non-zero exit. Shared by the branch-freeze-status
# workflow (rollout seed / manual re-sync) and the /freeze /unfreeze fan-out.
#
# Usage:  stamp-open-prs.sh [base-ref]      # blank base-ref = all open PRs
# Env:    GH_TOKEN (required), REPO (default: $GITHUB_REPOSITORY)
set -euo pipefail

base_ref="${1:-}"
export REPO="${REPO:-${GITHUB_REPOSITORY:?REPO or GITHUB_REPOSITORY must be set}}"
here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

args=(--repo "$REPO" --state open --limit 1000 --json "number,headRefOid,baseRefName")
if [ -n "$base_ref" ]; then
  args+=(--base "$base_ref")
fi

prs="$(gh pr list "${args[@]}")"
count="$(printf '%s' "$prs" | jq 'length')"
echo "Stamping ${count} open PR(s)${base_ref:+ targeting ${base_ref}}"
if [ "$count" -ge 1000 ]; then
  echo "::warning::Open PR count hit the query limit (${count}); some PRs may not have been stamped."
fi

fail=0
while IFS= read -r pr; do
  num="$(printf '%s' "$pr" | jq -r '.number')"
  sha="$(printf '%s' "$pr" | jq -r '.headRefOid')"
  base="$(printf '%s' "$pr" | jq -r '.baseRefName')"
  echo "::group::PR #${num} (${base} @ ${sha})"
  if ! bash "$here/post-freeze-status.sh" "$sha" "$base"; then
    echo "::warning::Failed to stamp PR #${num}"
    fail=1
  fi
  echo "::endgroup::"
done < <(printf '%s' "$prs" | jq -c '.[]')

exit "$fail"

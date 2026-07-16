#!/usr/bin/env bash
# Refreshes the `branch-freeze` status on every open pull request, optionally
# limited to a single base branch. Calls set-pr-status.sh once per PR and
# aggregates failures into a non-zero exit. Used by branch-freeze-refresh.yml
# for rollout seeding, manual re-sync, and /freeze or /unfreeze fan-out.
#
# Usage:  refresh-pr-statuses.sh [base-ref] # blank base-ref = all open PRs
# Env:    GH_TOKEN (required), REPO (default: $GITHUB_REPOSITORY)
set -euo pipefail

# Step 1: Read the optional branch filter and repository.
base_ref="${1:-}"
export REPO="${REPO:-${GITHUB_REPOSITORY:?REPO or GITHUB_REPOSITORY must be set}}"
here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Step 2: Build a query for open PRs, optionally limited to one base branch.
args=(--repo "$REPO" --state open --limit 1000 --json "number,headRefOid,baseRefName")
if [ -n "$base_ref" ]; then
  args+=(--base "$base_ref")
fi

# Step 3: Load the matching PRs and report how many will be refreshed.
prs="$(gh pr list "${args[@]}")"
count="$(printf '%s' "$prs" | jq 'length')"
echo "Stamping ${count} open PR(s)${base_ref:+ targeting ${base_ref}}"
if [ "$count" -ge 1000 ]; then
  echo "::warning::Open PR count hit the query limit (${count}); some PRs may not have been stamped."
fi

# Step 4: Refresh the required status on each PR's current head commit.
fail=0
while IFS= read -r pr; do
  num="$(printf '%s' "$pr" | jq -r '.number')"
  sha="$(printf '%s' "$pr" | jq -r '.headRefOid')"
  base="$(printf '%s' "$pr" | jq -r '.baseRefName')"
  echo "::group::PR #${num} (${base} @ ${sha})"

  # Remember any failure, but continue refreshing the remaining PRs.
  if ! bash "$here/set-pr-status.sh" "$sha" "$base"; then
    echo "::warning::Failed to stamp PR #${num}"
    fail=1
  fi
  echo "::endgroup::"
done < <(printf '%s' "$prs" | jq -c '.[]')

# Step 5: Fail the workflow if any PR status could not be refreshed.
exit "$fail"

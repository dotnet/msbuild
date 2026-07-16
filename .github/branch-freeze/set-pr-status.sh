#!/usr/bin/env bash
# Posts the `branch-freeze` commit status for a single pull request head commit.
#
# Usage:   set-pr-status.sh <head-sha> <base-ref>
# Env:     GH_TOKEN  (required) token with `statuses: write` + `issues: read`
#          REPO      (optional) owner/repo; defaults to $GITHUB_REPOSITORY
#
# A branch is considered FROZEN while an open issue labeled `branch-freeze`
# contains the marker `<!-- branch-freeze:<branch> -->` on a line by itself
# (surrounding whitespace / CR tolerated). The remaining issue body is used as
# the human-readable reason, and an optional `<!-- branch-freeze-by:<login> -->`
# marker names who froze it (surfaced in the status description). Matching the
# marker as a WHOLE LINE - not a substring - prevents an issue that merely
# mentions the marker from freezing a branch.
set -euo pipefail

# Step 1: Read the PR commit, target branch, and repository.
sha="${1:?head sha required}"
base_ref="${2:?base ref required}"
repo="${REPO:-${GITHUB_REPOSITORY:?REPO or GITHUB_REPOSITORY must be set}}"

# Step 2: Define the required check name and tracking issue marker.
context="branch-freeze"
label="branch-freeze"
marker="<!-- branch-freeze:${base_ref} -->"

# Step 3: Load all open branch-freeze tracking issues.
issues="$(gh issue list \
  --repo "$repo" \
  --label "$label" \
  --state open \
  --limit 200 \
  --json number,body,url)"

# Step 4: Find the tracking issue for this PR's target branch.
match="$(printf '%s' "$issues" \
  | jq -c --arg m "$marker" \
      'map(select((.body // "") | split("\n") | any(.[]; (gsub("^\\s+|\\s+$";"")) == $m)))
       | first // empty')"

if [ -n "$match" ]; then
  # Step 5a: The branch is frozen. Read the issue details for the check message.
  issue_url="$(printf '%s' "$match" | jq -r '.url')"
  body="$(printf '%s' "$match" | jq -r '.body // ""')"

  # Who froze it, from the machine-readable actor marker (if present).
  actor="$(printf '%s' "$body" \
    | sed -nE 's@^[[:space:]]*<!--[[:space:]]*branch-freeze-by:[[:space:]]*([^[:space:]>]+)[[:space:]]*-->[[:space:]]*$@\1@p' \
    | head -n1)"

  # Human-readable reason = body minus any branch-freeze marker lines.
  reason="$(printf '%s' "$body" \
    | grep -vE '^[[:space:]]*<!--[[:space:]]*branch-freeze.*-->[[:space:]]*$' \
    | tr '\n' ' ' \
    | sed -E 's/[[:space:]]+/ /g; s/^ //; s/ $//')"
  [ -n "$reason" ] || reason="(no reason provided)"

  if [ -n "$actor" ]; then
    description="Frozen by @${actor}: ${reason}"
  else
    description="Frozen: ${reason}"
  fi
  # GitHub truncates status descriptions at 140 chars; trim with an ellipsis.
  if [ "${#description}" -gt 140 ]; then
    description="${description:0:137}..."
  fi

  # Step 6a: Mark the required check as failed and link the tracking issue.
  echo "Branch '$base_ref' is FROZEN -> reporting failure on $sha"
  gh api -X POST "repos/$repo/statuses/$sha" \
    -f state=failure \
    -f context="$context" \
    -f description="$description" \
    -f target_url="$issue_url" >/dev/null
else
  # Step 5b: The branch is open, so mark the required check as successful.
  echo "Branch '$base_ref' is open -> reporting success on $sha"
  gh api -X POST "repos/$repo/statuses/$sha" \
    -f state=success \
    -f context="$context" \
    -f description="Branch open" >/dev/null
fi

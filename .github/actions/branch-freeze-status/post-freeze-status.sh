#!/usr/bin/env bash
# Posts the `branch-freeze` commit status for a single pull request head commit.
#
# Usage:   post-freeze-status.sh <head-sha> <base-ref>
# Env:     GH_TOKEN  (required) token with `statuses: write` + `issues: read`
#          REPO      (optional) owner/repo; defaults to $GITHUB_REPOSITORY
#
# A branch is considered FROZEN while an open issue labeled `branch-freeze`
# contains the marker `<!-- branch-freeze:<branch> -->` on a line by itself
# (surrounding whitespace / CR tolerated). The remaining issue body is used as
# the human-readable reason. Matching the marker as a WHOLE LINE - not a
# substring - prevents an issue that merely mentions the marker from freezing a
# branch.
set -euo pipefail

sha="${1:?head sha required}"
base_ref="${2:?base ref required}"
repo="${REPO:-${GITHUB_REPOSITORY:?REPO or GITHUB_REPOSITORY must be set}}"

context="branch-freeze"
label="branch-freeze"
marker="<!-- branch-freeze:${base_ref} -->"

issues="$(gh issue list \
  --repo "$repo" \
  --label "$label" \
  --state open \
  --limit 200 \
  --json number,body,url)"

match="$(printf '%s' "$issues" \
  | jq -c --arg m "$marker" \
      'map(select((.body // "") | split("\n") | any(.[]; (gsub("^\\s+|\\s+$";"")) == $m)))
       | first // empty')"

if [ -n "$match" ]; then
  issue_url="$(printf '%s' "$match" | jq -r '.url')"

  reason="$(printf '%s' "$match" \
    | jq -r --arg m "$marker" \
        '(.body // "") | split("\n") | map(select((gsub("^\\s+|\\s+$";"")) != $m)) | join("\n")' \
    | tr '\n' ' ' \
    | sed -E 's/[[:space:]]+/ /g; s/^ //; s/ $//')"
  [ -n "$reason" ] || reason="(no reason provided)"

  description="Frozen: ${reason}"
  # GitHub truncates status descriptions at 140 chars; trim with an ellipsis.
  if [ "${#description}" -gt 140 ]; then
    description="${description:0:137}..."
  fi

  echo "Branch '$base_ref' is FROZEN -> reporting failure on $sha"
  gh api -X POST "repos/$repo/statuses/$sha" \
    -f state=failure \
    -f context="$context" \
    -f description="$description" \
    -f target_url="$issue_url" >/dev/null
else
  echo "Branch '$base_ref' is open -> reporting success on $sha"
  gh api -X POST "repos/$repo/statuses/$sha" \
    -f state=success \
    -f context="$context" \
    -f description="Branch open" >/dev/null
fi

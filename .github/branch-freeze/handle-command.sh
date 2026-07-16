#!/usr/bin/env bash
# Handles a /freeze or /unfreeze command. Authorization is performed by the
# calling workflow BEFORE this script runs.
#
# Command is read from the FIRST line of the triggering comment ($BODY):
#   /freeze   [--branch <name>] <reason...>   branch defaults to `main`; reason required
#   /unfreeze [--branch <name>]               branch defaults to `main`
#
# Freeze state is stored as an open issue labeled `branch-freeze` whose body is
#   <reason>
#
#   <!-- branch-freeze:<branch> -->
# This script only toggles that state and replies; it emits `branch` and
# `changed` step outputs so the calling workflow can refresh the affected PR
# statuses via branch-freeze-refresh.yml.
#
# Env (required): GH_TOKEN, REPO, ACTOR, ISSUE_NUMBER, COMMENT_ID, BODY
set -euo pipefail

# Step 1: Read the values supplied by the command workflow.
: "${GH_TOKEN:?}"; : "${REPO:?}"; : "${ACTOR:?}"; : "${ISSUE_NUMBER:?}"; : "${COMMENT_ID:?}"
BODY="${BODY:-}"
label="branch-freeze"

# Step 2: Define helpers for reacting, replying, and returning workflow outputs.
react() { gh api -X POST "repos/$REPO/issues/comments/$COMMENT_ID/reactions" -f content="$1" >/dev/null 2>&1 || true; }
reply() {
  if ! gh issue comment "$ISSUE_NUMBER" --repo "$REPO" --body "$1" >/dev/null; then
    echo "::warning::Failed to post confirmation comment on issue #${ISSUE_NUMBER}."
  fi
  return 0
}
set_output() { [ -n "${GITHUB_OUTPUT:-}" ] && printf '%s=%s\n' "$1" "$2" >> "$GITHUB_OUTPUT"; return 0; }

# Backticks below are literal Markdown in the reply text, not command substitution.
# shellcheck disable=SC2016
usage='**Usage**
- `/freeze [--branch <name>] <reason>` — freeze a branch (default `main`); a reason is required.
- `/unfreeze [--branch <name>]` — unfreeze a branch (default `main`).'

fail_usage() { react "confused"; reply "$1"$'\n\n'"$usage"; exit 0; }

# Step 3: Read the command and arguments from the comment's first line.
line="$(printf '%s' "$BODY" | head -n1 | tr -d '\r')"
cmd="${line%%[[:space:]]*}"
rest="${line#"$cmd"}"
rest="${rest#"${rest%%[![:space:]]*}"}"   # left-trim

# Step 4: Decide whether this is a freeze or unfreeze request.
case "$cmd" in
  /freeze)   action="freeze" ;;
  /unfreeze) action="unfreeze" ;;
  *) echo "Ignoring non-command first token: '$cmd'"; exit 0 ;;
esac

# Step 5: Read the optional branch and the freeze reason.
branch="main"
case "$rest" in
  --branch|-b)
    fail_usage "Missing a branch name after \`${rest}\`." ;;
  --branch[[:space:]]*|-b[[:space:]]*)
    branch="$(printf '%s' "$rest" | awk '{print $2}')"
    rest="$(printf '%s' "$rest" | sed -E 's/^(--branch|-b)[[:space:]]+[^[:space:]]+[[:space:]]*//')"
    ;;
esac
reason="$(printf '%s' "$rest" | sed -E 's/[[:space:]]+$//')"
[ -n "$branch" ] || fail_usage "No branch name was given after the \`--branch\` flag."

# Step 6: Validate the branch name and confirm that the branch exists.
case "$branch" in
  *[!A-Za-z0-9._/-]*) fail_usage "Branch \`$branch\` contains unexpected characters." ;;
esac

if ! gh api "repos/$REPO/branches/$branch" >/dev/null 2>&1; then
  fail_usage "Branch \`$branch\` was not found in \`$REPO\`."
fi

marker="<!-- branch-freeze:$branch -->"

# Step 7: Ensure the tracking label exists.
gh label create "$label" --repo "$REPO" --color B60205 \
  --description "Tracks a frozen branch" >/dev/null 2>&1 || true

# Step 8: Find any open tracking issues for this branch.
existing_nums="$(gh issue list --repo "$REPO" --label "$label" --state open --limit 200 --json number,body \
  | jq -r --arg m "$marker" \
      'map(select((.body // "") | split("\n") | any(.[]; (gsub("^\\s+|\\s+$";"")) == $m))) | .[].number')"

if [ "$action" = "freeze" ]; then
  # Step 9a: Freeze the branch by creating or updating its tracking issue.
  [ -n "$reason" ] || fail_usage "A reason is required to freeze \`$branch\`."

  # The branch marker makes the issue machine-detectable; the actor marker records
  # who froze the branch so the blocking status can name them. Both marker lines
  # are stripped from the human-readable reason by set-pr-status.sh.
  issue_body="${reason}"$'\n\n'"${marker}"$'\n'"<!-- branch-freeze-by:${ACTOR} -->"

  # Keep one tracking issue and close any duplicates.
  primary=""
  while IFS= read -r n; do
    [ -n "$n" ] || continue
    if [ -z "$primary" ]; then
      primary="$n"
    else
      gh issue close "$n" --repo "$REPO" \
        --comment "Superseded by #${primary} (duplicate \`$branch\` freeze tracking issue)." >/dev/null || true
    fi
  done <<< "$existing_nums"

  if [ -n "$primary" ]; then
    gh issue edit "$primary" --repo "$REPO" --body "$issue_body" >/dev/null
    issue_num="$primary"; verb="updated"
  else
    url="$(gh issue create --repo "$REPO" --label "$label" \
      --title "Branch frozen: $branch" --body "$issue_body")"
    issue_num="${url##*/}"; verb="opened"
  fi

  # Tell the workflow to refresh PR statuses, then acknowledge the command.
  set_output branch "$branch"
  set_output changed "true"
  react "+1"
  reply "❄️ **\`$branch\` is now frozen** by @${ACTOR} — tracking issue #${issue_num} (${verb}).

> ${reason}

Pull requests targeting \`${branch}\` will be blocked by the \`branch-freeze\` check until someone runs \`/unfreeze --branch ${branch}\` (or \`/unfreeze\` for \`main\`)."
else
  # Step 9b: Unfreeze the branch by closing its tracking issues.
  closed=""
  while IFS= read -r n; do
    [ -n "$n" ] || continue
    gh issue close "$n" --repo "$REPO" \
      --comment "Unfrozen by @${ACTOR} via \`/unfreeze\`." >/dev/null
    closed="${closed:+$closed, }#${n}"
  done <<< "$existing_nums"

  if [ -z "$closed" ]; then
    # The branch was already open, so no PR status refresh is needed.
    set_output changed "false"
    react "+1"
    reply "\`${branch}\` is not currently frozen — nothing to do."
    exit 0
  fi

  # Tell the workflow to refresh PR statuses, then acknowledge the command.
  set_output branch "$branch"
  set_output changed "true"
  react "+1"
  reply "✅ **\`${branch}\` is now unfrozen** by @${ACTOR} — closed tracking issue(s) ${closed}. The \`branch-freeze\` check now passes on open PRs targeting \`${branch}\`."
fi

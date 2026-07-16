# Branch freeze (merge freeze)

When something is broken downstream of MSBuild (for example a bad SDK insertion or
a VS update), the on-duty committer (the [`@dotnet/kitten`](https://github.com/orgs/dotnet/teams/kitten)
build-duty role) can **freeze** a branch so that no new pull requests merge until
the problem is resolved, then **unfreeze** it. This replaces the previous informal
"please hold checkins" message over Teams (see [dotnet/msbuild#13589](https://github.com/dotnet/msbuild/issues/13589)).

## TL;DR for committers

Comment on any issue or pull request:

| Command | Effect |
|---|---|
| `/freeze <reason>` | Freeze `main` with a reason. |
| `/freeze --branch vs17.14 <reason>` | Freeze a specific branch (e.g. a release branch). |
| `/unfreeze` | Unfreeze `main`. |
| `/unfreeze --branch vs17.14` | Unfreeze a specific branch. |

* A reason is **required** to freeze; it is shown on every blocked PR.
* Only accounts listed in [`.github/branch-freeze-allowlist.txt`](../.github/branch-freeze-allowlist.txt) can run these commands; anyone else gets a polite refusal.
* The bot reacts 👍 and replies with a confirmation (or 😕 with usage help if the command is malformed).

While a branch is frozen, every open and new PR targeting it shows a failing
**`branch-freeze`** check — `Frozen by @<login>: <reason>` — that links to the
tracking issue and blocks merge. `/unfreeze` turns the check green again.

## How it works

* **Tracking issue state** lives in a GitHub issue labeled `branch-freeze`, one per frozen branch.
  The issue body holds the reason plus marker lines `<!-- branch-freeze:<branch> -->`
  and `<!-- branch-freeze-by:<login> -->` (who froze it).
  `/freeze` opens/updates it; `/unfreeze` closes it. The issue is the audit trail.
* **Enforcement** is the `branch-freeze` commit status, a required status check in
  the repository ruleset. The [`branch-freeze-pr-status`](../.github/workflows/branch-freeze-pr-status.yml)
  workflow evaluates one changed PR; `edited` covers a PR being **retargeted** onto
  a frozen branch. The [`branch-freeze-refresh`](../.github/workflows/branch-freeze-refresh.yml)
  workflow refreshes existing open PRs after `/freeze` or `/unfreeze`, and supports
  manual rollout seeding or repair.
* **The command** ([`branch-freeze-command`](../.github/workflows/branch-freeze-command.yml))
  verifies the commenter is on the allowlist, toggles the tracking issue, then
  refreshes the affected branch's PR statuses. All status writers
  for a branch share a `branch-freeze-write-<branch>` concurrency group, so a freeze
  fan-out can never be overwritten by a concurrent per-PR stamp.

### Files

| File | Role |
|---|---|
| [`.github/workflows/branch-freeze-command.yml`](../.github/workflows/branch-freeze-command.yml) | Processes `/freeze` and `/unfreeze` and changes tracking issue state |
| [`.github/workflows/branch-freeze-pr-status.yml`](../.github/workflows/branch-freeze-pr-status.yml) | Evaluates the `branch-freeze` status for one changed PR |
| [`.github/workflows/branch-freeze-refresh.yml`](../.github/workflows/branch-freeze-refresh.yml) | Refreshes the status on existing open PRs |
| [`.github/workflows/branch-freeze-tests.yml`](../.github/workflows/branch-freeze-tests.yml) | Runs shellcheck + the shell unit tests on branch-freeze changes |
| [`.github/branch-freeze-allowlist.txt`](../.github/branch-freeze-allowlist.txt) | GitHub logins allowed to run `/freeze` `/unfreeze` |
| [`.github/branch-freeze/`](../.github/branch-freeze/) | Shared command and status scripts |

### Tests

The shell scripts are unit-tested with a mock `gh` (no live repo required). The
[`branch-freeze-tests`](../.github/workflows/branch-freeze-tests.yml) workflow runs
them (plus a shellcheck pass) on any change under the branch-freeze paths. Run them
locally with `jq` installed:

```bash
bash .github/branch-freeze/tests/run-tests.sh
```

## One-time setup (admin)

Enforcement is intentionally **not** active until an admin completes these steps.
Do them in order — making the check required *before* seeding existing PRs would
block every open PR.

1. **Label** — create the `branch-freeze` label (the command also creates it on
   first use, but creating it up front is cleaner):
   ```bash
   gh label create branch-freeze -c B60205 -d "Tracks a frozen branch"
   ```
2. **Allowlist** — populate [`.github/branch-freeze-allowlist.txt`](../.github/branch-freeze-allowlist.txt)
   with the GitHub logins allowed to freeze/unfreeze (one per line; `#` comments
   allowed). Changes go through normal PR review, which is the audit trail for who
   may operate the freeze. No GitHub App or org-level permissions are required.
3. **Merge the workflows** (this PR). At this point nothing is enforced yet.
4. **Seed existing PRs** so they all carry a green status before the check becomes
   required: run the `branch-freeze-refresh` workflow via **Actions → Run workflow**
   (leave `base_ref` blank to stamp all open PRs). Confirm a few PRs show a green
   `branch-freeze` check.
5. **End-to-end test** on a throwaway branch/PR: `/freeze --branch <test> testing`
   → the PR shows red `branch-freeze`; `/unfreeze --branch <test>` → green.
6. **Make it required** — add `branch-freeze` to the required status checks of the
   relevant repository ruleset(s):
   * `Basic checkin policy` (covers `main` + `vs*.*`)
   * `Release branches` (covers `vs*`), if release branches should be freezable.

   Pin the check to the GitHub Actions app integration id to reject spoofed
   statuses, consistent with the existing `msbuild-pr` / `license/cla` entries.

### Rollback / disable

Remove the `branch-freeze` context from the ruleset's required checks — enforcement
stops immediately. Close any open `branch-freeze` issues to clear residual statuses
(or run the bulk stamp again).

### Notes

* The bulk seed in step 4 (blank `base_ref`) is a one-time/manual operation; run it
  when no `/freeze` `/unfreeze` is in flight, since the all-branches seed is not
  serialized with per-branch operations.
* The fan-out re-stamps up to 1000 open PRs per branch; if a repo ever exceeds that,
  the workflow logs a warning to raise the limit.

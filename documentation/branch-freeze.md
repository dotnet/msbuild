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
* Only members of `@dotnet/kitten` can run these commands; anyone else gets a polite refusal.
* The bot reacts 👍 and replies with a confirmation (or 😕 with usage help if the command is malformed).

While a branch is frozen, every open and new PR targeting it shows a failing
**`branch-freeze`** check — `❄️ Frozen: <reason>` — that links to the tracking
issue and blocks merge. `/unfreeze` turns the check green again.

## How it works

* **State** lives in a GitHub issue labeled `branch-freeze`, one per frozen branch.
  The issue body holds the reason plus a marker line `<!-- branch-freeze:<branch> -->`.
  `/freeze` opens/updates it; `/unfreeze` closes it. The issue is the audit trail.
* **Enforcement** is the `branch-freeze` commit status, a required status check in
  the repository ruleset. The [`branch-freeze-status`](../.github/workflows/branch-freeze-status.yml)
  workflow stamps it:
  * `pull_request_target` (opened / synchronize / reopened / **edited**) stamps the
    single PR that changed — `edited` covers a PR being **retargeted** onto a frozen branch.
  * `workflow_dispatch` / `workflow_call` bulk-stamp open PRs (rollout seed and the
    `/freeze` `/unfreeze` fan-out).
* **The command** ([`branch-freeze-command`](../.github/workflows/branch-freeze-command.yml))
  verifies kitten membership, toggles the tracking issue, then re-stamps the
  affected branch's PRs through the status workflow. All status writers for a branch
  share a `branch-freeze-write-<branch>` concurrency group, so a freeze fan-out can
  never be overwritten by a concurrent per-PR stamp.

### Files

| File | Role |
|---|---|
| [`.github/workflows/branch-freeze-command.yml`](../.github/workflows/branch-freeze-command.yml) | `/freeze` `/unfreeze` entry point + kitten auth + fan-out |
| [`.github/workflows/branch-freeze-status.yml`](../.github/workflows/branch-freeze-status.yml) | Stamps the `branch-freeze` status (single PR + bulk) |
| [`.github/actions/branch-freeze-status/`](../.github/actions/branch-freeze-status/) | Composite action + shared scripts (`post-freeze-status.sh`, `stamp-open-prs.sh`, `freeze-command.sh`) |

## One-time setup (admin)

Enforcement is intentionally **not** active until an admin completes these steps.
Do them in order — making the check required *before* seeding existing PRs would
block every open PR.

1. **Label** — create the `branch-freeze` label (the command also creates it on
   first use, but creating it up front is cleaner):
   ```bash
   gh label create branch-freeze -c B60205 -d "Tracks a frozen branch"
   ```
2. **GitHub App** for the kitten membership check (the workflow `GITHUB_TOKEN`
   cannot read org team membership):
   * Register/identify a GitHub App installed on the `dotnet` org with
     **Organization → Members: Read**.
   * Add repo secrets **`BRANCH_FREEZE_APP_ID`** and **`BRANCH_FREEZE_APP_PRIVATE_KEY`**.
3. **Merge the workflows** (this PR). At this point nothing is enforced yet.
4. **Seed existing PRs** so they all carry a green status before the check becomes
   required: run the `branch-freeze-status` workflow via **Actions → Run workflow**
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
